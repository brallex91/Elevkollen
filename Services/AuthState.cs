using Microsoft.JSInterop;

namespace Elevkollen.Services;

/// <summary>
/// Enkel inloggningsspärr med hårdkodade uppgifter.
///
/// OBS: Detta är en platshållare, inte säkerhet — all data ligger ändå lokalt i
/// webbläsaren. Byt till riktig autentisering innan appen hostas.
/// </summary>
public sealed class AuthState(IJSRuntime js)
{
    private const string Username = "demo";
    private const string Password = "demo";
    private const string StorageKey = "Elevkollen.auth";

    public bool IsLoggedIn { get; private set; }

    public event Action? Changed;

    /// <summary>Läser sparad inloggning från localStorage vid uppstart.</summary>
    public async Task InitializeAsync()
    {
        var value = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        SetLoggedIn(value == "1");
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        if (username != Username || password != Password)
        {
            return false;
        }

        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, "1");
        SetLoggedIn(true);
        return true;
    }

    public async Task LogoutAsync()
    {
        await js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        SetLoggedIn(false);
    }

    private void SetLoggedIn(bool value)
    {
        if (IsLoggedIn == value)
        {
            return;
        }

        IsLoggedIn = value;
        Changed?.Invoke();
    }
}
