using Microsoft.JSInterop;

namespace Elevkollen.Services;

/// <summary>
/// Simple sign-in gate with hard-coded credentials.
///
/// NOTE: this is a placeholder, not security — all data lives locally in the browser
/// anyway. Replace with real authentication before hosting the app.
/// </summary>
public sealed class AuthState(IJSRuntime js)
{
    private const string Username = "demo";
    private const string Password = "demo";
    private const string StorageKey = "Elevkollen.auth";

    public bool IsLoggedIn { get; private set; }

    public event Action? Changed;

    /// <summary>Reads the saved sign-in from localStorage at startup.</summary>
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
