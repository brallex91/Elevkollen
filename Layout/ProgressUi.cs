using MudBlazor;
using Elevkollen.Shared;

namespace Elevkollen.Layout;

/// <summary>
/// UI-färgerna för elevens utveckling. Ligger i klienten eftersom <see cref="Color"/>
/// kommer från MudBlazor — texterna själva bor i <see cref="ProgressText"/>.
/// </summary>
public static class ProgressUi
{
    public static Color Color(this Progress progress) => progress switch
    {
        Progress.Achieved => MudBlazor.Color.Success,
        Progress.InProgress => MudBlazor.Color.Warning,
        _ => MudBlazor.Color.Error,
    };
}
