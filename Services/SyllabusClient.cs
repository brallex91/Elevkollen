using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using Elevkollen.Shared;

namespace Elevkollen.Services;

/// <summary>
/// Hämtar läroplaner direkt från Skolverkets öppna API och renskriver dem i webbläsaren.
/// Ingen egen server är inblandad, och ingen persondata lämnar någonsin enheten.
/// Basadressen sätts i wwwroot/appsettings.json.
///
/// Varje lyckat svar cachas i IndexedDB. Går nätet inte att nå används den senast
/// hämtade kopian, så att läraren kan fortsätta arbeta offline.
/// </summary>
public sealed class SyllabusClient(HttpClient http, IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _db;

    private async ValueTask<IJSObjectReference> DbAsync() =>
        _db ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/db.js");

    /// <summary>Sant när senaste anropet besvarades från den lokala cachen.</summary>
    public bool ServedFromCache { get; private set; }

    /// <summary>Grundskolans ämnen, sorterade på namn.</summary>
    public Task<IReadOnlyList<SubjectDto>> GetSubjectsAsync() =>
        GetCachedAsync("syllabus:subjects", [], async () =>
        {
            var response = await http.GetFromJsonAsync<SubjectListResponse>(
                "subjects?schoolType=GR&timespan=LATEST");

            return (response?.Subjects ?? [])
                .Where(s => s.SchoolTypes.Contains("GR") && !string.IsNullOrWhiteSpace(s.Name))
                .Select(s => new SubjectDto(s.Code, s.Name))
                .OrderBy(s => s.Name, StringComparer.CurrentCulture)
                .ToArray() as IReadOnlyList<SubjectDto>;
        });

    /// <summary>Ett ämnes centrala innehåll och betygskriterier, renskrivna.</summary>
    public Task<SyllabusDto?> GetSyllabusAsync(string code) =>
        GetCachedAsync<SyllabusDto?>($"syllabus:{code}", null, async () =>
        {
            var response = await http.GetFromJsonAsync<SubjectResponse>(
                $"subjects/{Uri.EscapeDataString(code)}?timespan=LATEST");

            if (response?.Subject is not { } s)
            {
                return null;
            }

            var contents = s.CentralContents
                .SelectMany(cc => SyllabusTextService
                    .SplitCentralContent(cc.Text)
                    .Select(g => new CentralContentGroupDto(cc.Year ?? "", g.Heading, g.Items)))
                .ToArray();

            var criteria = s.KnowledgeRequirements
                .Where(kr => SyllabusTextService.IsSelectableGradeStep(kr.GradeStep))
                .Select(kr => new GradingCriterionDto(
                    int.TryParse(kr.Year, out var y) ? y : 0,
                    kr.GradeStep ?? "",
                    SyllabusTextService.CleanCriterion(kr.Text)))
                .Where(c => c.Text.Length > 0)
                .OrderBy(c => c.Year).ThenBy(c => c.GradeStep)
                .ToArray();

            return new SyllabusDto(s.Code, s.Name, contents, criteria);
        });

    /// <summary>
    /// Kör hämtningen, cachar resultatet och faller tillbaka på den lokala kopian
    /// när Skolverket inte går att nå.
    /// </summary>
    private async Task<T> GetCachedAsync<T>(string key, T fallback, Func<Task<T>> fetch)
    {
        try
        {
            var fresh = await fetch();
            ServedFromCache = false;

            if (fresh is not null)
            {
                await SetCacheAsync(key, JsonSerializer.Serialize(fresh));
                return fresh;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Nätverket eller Skolverket är otillgängligt — vi faller tillbaka nedan.
        }

        var cached = await GetCacheAsync(key);
        ServedFromCache = cached is not null;

        return cached is null ? fallback : JsonSerializer.Deserialize<T>(cached) ?? fallback;
    }

    private async Task<string?> GetCacheAsync(string key)
    {
        try
        {
            var db = await DbAsync();
            return await db.InvokeAsync<string?>("getMeta", key);
        }
        catch (JSException)
        {
            return null;
        }
    }

    private async Task SetCacheAsync(string key, string value)
    {
        try
        {
            var db = await DbAsync();
            await db.InvokeVoidAsync("setMeta", key, value);
        }
        catch (JSException)
        {
            // En misslyckad cachning får aldrig stoppa ett lyckat anrop.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            try
            {
                await _db.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Sidan är redan stängd — inget att städa.
            }
        }
    }

    // Skolverkets svarsformat — endast fälten vi faktiskt använder.
    private sealed record SubjectListResponse(
        [property: JsonPropertyName("subjects")] SubjectSummary[] Subjects);

    private sealed record SubjectSummary(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("schoolTypes")] string[] SchoolTypes);

    private sealed record SubjectResponse(
        [property: JsonPropertyName("subject")] SubjectDetail? Subject);

    private sealed record SubjectDetail(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("centralContents")] CentralContentRaw[] CentralContents,
        [property: JsonPropertyName("knowledgeRequirements")] KnowledgeRequirementRaw[] KnowledgeRequirements);

    private sealed record CentralContentRaw(
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("year")] string? Year);

    private sealed record KnowledgeRequirementRaw(
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("year")] string? Year,
        [property: JsonPropertyName("gradeStep")] string? GradeStep);
}
