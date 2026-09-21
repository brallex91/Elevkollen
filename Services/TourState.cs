using Microsoft.JSInterop;

namespace Elevkollen.Services;

/// <summary>
/// Tracks whether the user has seen the intro tour. The choice is stored in localStorage
/// and is a pure UI setting — no personal data is involved.
/// </summary>
public sealed class TourState(IJSRuntime js)
{
    private const string StorageKey = "Elevkollen.tour";

    /// <summary>True when the tour should show, i.e. the first visit in this browser.</summary>
    public bool ShouldShow { get; private set; }

    public event Action? Changed;

    public async Task InitializeAsync()
    {
        var seen = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        SetShouldShow(seen != "1");
    }

    /// <summary>Ends the tour. <paramref name="remember"/> stops it from showing again.</summary>
    public async Task CompleteAsync(bool remember)
    {
        if (remember)
        {
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey, "1");
        }

        SetShouldShow(false);
    }

    /// <summary>Restarts the tour, e.g. from a help button.</summary>
    public async Task RestartAsync()
    {
        await js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        SetShouldShow(true);
    }

    private void SetShouldShow(bool value)
    {
        if (ShouldShow == value)
        {
            return;
        }

        ShouldShow = value;
        Changed?.Invoke();
    }
}
