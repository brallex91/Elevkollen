using Microsoft.JSInterop;

namespace Elevkollen.Services;

/// <summary>
/// Håller reda på om användaren har sett introduktionsguiden. Valet sparas i localStorage
/// och är en ren UI-inställning — ingen persondata är inblandad.
/// </summary>
public sealed class TourState(IJSRuntime js)
{
    private const string StorageKey = "Elevkollen.tour";

    /// <summary>Sant när guiden ska visas, dvs. första besöket i den här webbläsaren.</summary>
    public bool ShouldShow { get; private set; }

    public event Action? Changed;

    public async Task InitializeAsync()
    {
        var seen = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        SetShouldShow(seen != "1");
    }

    /// <summary>Avslutar guiden. <paramref name="remember"/> hindrar den från att visas igen.</summary>
    public async Task CompleteAsync(bool remember)
    {
        if (remember)
        {
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey, "1");
        }

        SetShouldShow(false);
    }

    /// <summary>Startar guiden på nytt, t.ex. från en hjälpknapp.</summary>
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
