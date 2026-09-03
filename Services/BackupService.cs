using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Elevkollen.Services;

/// <summary>Fel som går att förklara för användaren utan teknisk jargong.</summary>
public sealed class BackupException(string message) : Exception(message);

/// <summary>
/// Krypterad säkerhetskopia av all elevdata. Filen krypteras med AES-256-GCM där nyckeln
/// härleds från användarens lösenord — utan lösenordet går den inte att läsa i något program.
/// </summary>
public sealed class BackupService(IJSRuntime js) : IAsyncDisposable
{
    private const string LastExportKey = "lastExport";

    /// <summary>Efter så här många dagar utan export påminner appen om att ta en kopia.</summary>
    public const int ReminderAfterDays = 14;

    private IJSObjectReference? _db;
    private IJSObjectReference? _crypto;

    private async ValueTask<IJSObjectReference> DbAsync() =>
        _db ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/db.js");

    private async ValueTask<IJSObjectReference> CryptoAsync() =>
        _crypto ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/crypto.js");

    public async Task<(int Students, int Assessments)> GetCountsAsync()
    {
        var db = await DbAsync();
        var counts = await db.InvokeAsync<Counts>("counts");
        return (counts.Students, counts.Assessments);
    }

    public async Task<string> SuggestPasswordAsync()
    {
        var crypto = await CryptoAsync();
        return await crypto.InvokeAsync<string>("suggestPassword");
    }

    public async Task ExportAsync(string password)
    {
        var db = await DbAsync();
        var crypto = await CryptoAsync();

        var json = await db.InvokeAsync<string>("exportAll");
        var fileName = $"elevdokumentation-{DateTime.Now:yyyy-MM-dd}.edok";

        await crypto.InvokeVoidAsync("exportEncrypted", json, password, fileName);
        await db.InvokeVoidAsync("setMeta", LastExportKey, DateTime.Today.ToString("O"));
    }

    /// <summary>Antal dagar sedan senaste export, eller null om ingen export gjorts.</summary>
    public async Task<int?> DaysSinceExportAsync()
    {
        var db = await DbAsync();
        var raw = await db.InvokeAsync<string?>("getMeta", LastExportKey);

        return DateTime.TryParse(raw, out var last)
            ? Math.Max(0, (DateTime.Today - last.Date).Days)
            : null;
    }

    public async Task<(int Students, int Assessments)> ImportAsync(ElementReference fileInput, string password)
    {
        var db = await DbAsync();
        var crypto = await CryptoAsync();

        string json;
        try
        {
            json = await crypto.InvokeAsync<string>("importEncrypted", fileInput, password);
        }
        catch (JSException ex)
        {
            throw new BackupException(ex.Message switch
            {
                var m when m.Contains("FORMAT") => "Filen är inte en giltig säkerhetskopia.",
                var m when m.Contains("VERSION") => "Filen kommer från en nyare version av appen.",
                _ => "Fel lösenord, eller så har filen skadats.",
            });
        }

        try
        {
            var result = await db.InvokeAsync<Counts>("importAll", json);
            // Datan kommer från en fil som användaren redan har, så den räknas som säkerhetskopierad.
            await db.InvokeVoidAsync("setMeta", LastExportKey, DateTime.Today.ToString("O"));
            return (result.Students, result.Assessments);
        }
        catch (JSException)
        {
            throw new BackupException("Filen kunde läsas men innehållet gick inte att tolka.");
        }
    }

    public async Task ClearFileInputAsync(ElementReference fileInput)
    {
        var crypto = await CryptoAsync();
        await crypto.InvokeVoidAsync("clearFileInput", fileInput);
    }

    public async Task<bool> HasFileAsync(ElementReference fileInput)
    {
        var crypto = await CryptoAsync();
        return await crypto.InvokeAsync<bool>("hasFile", fileInput);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var module in new[] { _db, _crypto })
        {
            if (module is not null)
            {
                try
                {
                    await module.DisposeAsync();
                }
                catch (JSDisconnectedException)
                {
                    // Sidan är redan stängd — inget att städa.
                }
            }
        }
    }

    private sealed record Counts(int Students, int Assessments);
}
