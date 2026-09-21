using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Elevkollen.Services;

/// <summary>An error that can be explained to the user without technical jargon.</summary>
public sealed class BackupException(string message) : Exception(message);

/// <summary>
/// Encrypted backup of all student data. The file is encrypted with AES-256-GCM using a
/// key derived from the user's password — without the password no program can read it.
/// </summary>
public sealed class BackupService(IJSRuntime js) : IAsyncDisposable
{
    private const string LastExportKey = "lastExport";

    /// <summary>After this many days without an export the app reminds the user to back up.</summary>
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
        var fileName = $"elevkollen-{DateTime.Now:yyyy-MM-dd}.edok";

        await crypto.InvokeVoidAsync("exportEncrypted", json, password, fileName);
        await db.InvokeVoidAsync("setMeta", LastExportKey, DateTime.Today.ToString("O"));
    }

    /// <summary>Days since the last export, or null if no export has been made.</summary>
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
            // The data comes from a file the user already has, so it counts as backed up.
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
                    // The page is already closed — nothing to clean up.
                }
            }
        }
    }

    private sealed record Counts(int Students, int Assessments);
}
