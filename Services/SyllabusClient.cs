using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using Elevkollen.Shared;

namespace Elevkollen.Services;

/// <summary>
/// Loads syllabuses directly from Skolverket's open API and cleans them up in the
/// browser. No server of our own is involved, and no personal data ever leaves the
/// device. The base address is set in wwwroot/appsettings.json.
///
/// Every successful response is cached in IndexedDB. If the network is unreachable the
/// most recently fetched copy is used, so the teacher can keep working offline.
/// </summary>
public sealed class SyllabusClient(HttpClient http, IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _db;

    private async ValueTask<IJSObjectReference> DbAsync() =>
        _db ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/db.js");

    /// <summary>True when the last call was answered from the local cache.</summary>
    public bool ServedFromCache { get; private set; }

    /// <summary>Compulsory school subjects, sorted by name.</summary>
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

    /// <summary>A subject's central content and grading criteria, cleaned up.</summary>
    public Task<SyllabusDto?> GetSyllabusAsync(string code) =>
        GetCachedAsync<SyllabusDto?>($"syllabus:v2:{code}", null, async () =>
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
                .SelectMany(kr => SyllabusTextService
                    .SplitCriteria(kr.Text)
                    .Select((c, i) => new CriterionDto(
                        int.TryParse(kr.Year, out var y) ? y : 0,
                        i,
                        kr.GradeStep ?? "",
                        c.Text,
                        c.ValueWords)))
                .Where(c => c.Text.Length > 0)
                .OrderBy(c => c.Year).ThenBy(c => c.Index).ThenBy(c => c.GradeStep)
                .ToArray();

            return new SyllabusDto(s.Code, s.Name, contents, criteria);
        });

    /// <summary>
    /// Runs the fetch, caches the result and falls back to the local copy when
    /// Skolverket is unreachable.
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
            // The network or Skolverket is unavailable — we fall back below.
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
            // A failed cache write must never break a successful call.
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
                // The page is already closed — nothing to clean up.
            }
        }
    }

    // Skolverket's response format — only the fields we actually use.
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
