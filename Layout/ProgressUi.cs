using MudBlazor;
using Elevkollen.Shared;

namespace Elevkollen.Layout;

/// <summary>
/// UI colors for student progress. Lives in the client because <see cref="Color"/>
/// comes from MudBlazor — the texts themselves live in <see cref="ProgressText"/>.
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
