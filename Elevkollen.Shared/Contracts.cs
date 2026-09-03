namespace Elevkollen.Shared;

/// <summary>Elevens utveckling för en bedömning.</summary>
public enum Progress
{
    NotAchieved = 0,
    InProgress = 1,
    Achieved = 2,
}

/// <summary>Domänspråket för utveckling, på ett enda ställe.</summary>
public static class ProgressText
{
    /// <summary>Betygsstegen läraren kan välja mellan i UI.</summary>
    public static readonly string[] GradeSteps = ["A", "B", "C", "D", "E", "F"];

    /// <summary>Utvecklingsalternativen i den ordning de visas för läraren.</summary>
    public static readonly Progress[] All =
        [Progress.NotAchieved, Progress.InProgress, Progress.Achieved];

    public static string Label(this Progress progress) => progress switch
    {
        Progress.Achieved => "Uppnått",
        Progress.InProgress => "Pågående",
        _ => "Ej uppnått",
    };

    /// <summary>Kort symbol för trånga celler i klassöversikten.</summary>
    public static string Symbol(this Progress progress) => progress switch
    {
        Progress.Achieved => "✓",
        Progress.InProgress => "~",
        _ => "✕",
    };
}

// ---------- Elever ----------

public sealed record StudentDto(
    int Id,
    string Name,
    string? Email,
    string? GuardianContact,
    int? SchoolYear,
    string? ClassName,
    int AssessmentCount,
    DateOnly? LastAssessmentDate);

public sealed record StudentDetailDto(
    int Id,
    string Name,
    string? Email,
    string? GuardianContact,
    int? SchoolYear,
    string? ClassName,
    IReadOnlyList<AssessmentDto> Assessments);

public sealed record SaveStudentRequest(
    string Name,
    string? Email,
    string? GuardianContact,
    int? SchoolYear,
    string? ClassName);

/// <summary>
/// Klassbeteckningen skrivs som årskurs + klass, t.ex. "4B". Lärare skriver bara in
/// bokstaven, så årskursen sätts framför när den finns och inte redan står där.
/// </summary>
public static class ClassLabel
{
    public static string For(int? schoolYear, string? className)
    {
        var name = className?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            return schoolYear is null ? "" : $"Årskurs {schoolYear}";
        }

        return schoolYear is null || char.IsDigit(name[0]) ? name : $"{schoolYear}{name}";
    }

    /// <summary>
    /// Städar lärarens inmatning: trimmar, slår ihop dubbla mellanslag och ger varje ord
    /// stor begynnelsebokstav. "andra klassen" blir "Andra Klassen" och "a" blir "A".
    /// </summary>
    public static string? Normalize(string? className)
    {
        if (string.IsNullOrWhiteSpace(className))
        {
            return null;
        }

        var words = className.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(w => char.ToUpper(w[0]) + w[1..]);

        return string.Join(' ', words);
    }
}

// ---------- Bedömningar ----------

public sealed record AssessmentDto(
    int Id,
    int StudentId,
    string SubjectCode,
    string SubjectName,
    string? WorkArea,
    string? CentralContent,
    string? GradingCriterion,
    string? GradeStep,
    Progress Progress,
    string? Comment,
    DateOnly Date);

public sealed record SaveAssessmentRequest(
    int StudentId,
    string SubjectCode,
    string SubjectName,
    string? WorkArea,
    string? CentralContent,
    string? GradingCriterion,
    string? GradeStep,
    Progress Progress,
    string? Comment,
    DateOnly Date);

// ---------- Statistik ----------

/// <summary>Utveckling över tid, per ämne, för en elev.</summary>
public sealed record StudentStatsDto(
    int StudentId,
    IReadOnlyList<SubjectProgressDto> Subjects,
    IReadOnlyList<ProgressPointDto> Timeline);

public sealed record SubjectProgressDto(
    string SubjectCode,
    string SubjectName,
    int AssessmentCount,
    int Achieved,
    int InProgress,
    int NotAchieved,
    string? LatestGradeStep);

public sealed record ProgressPointDto(
    DateOnly Date,
    string SubjectName,
    Progress Progress,
    string? GradeStep);

// ---------- Klassöversikt ----------

/// <summary>Matris över en klass: elever på raderna, arbetsområden på kolumnerna.</summary>
public sealed record ClassOverviewDto(
    IReadOnlyList<OverviewColumnDto> Columns,
    IReadOnlyList<OverviewRowDto> Rows);

public sealed record OverviewColumnDto(
    string SubjectCode,
    string SubjectName,
    string WorkArea);

/// <summary>Cells index följer Columns. Null betyder att eleven saknar bedömning där.</summary>
public sealed record OverviewRowDto(
    int StudentId,
    string StudentName,
    string? ClassName,
    int? SchoolYear,
    IReadOnlyList<OverviewCellDto?> Cells);

public sealed record OverviewCellDto(
    Progress Progress,
    string? GradeStep,
    DateOnly Date,
    int AssessmentCount);

// ---------- Startsida ----------

/// <summary>Sammanställning av all lokal data, för startsidans diagram och nyckeltal.</summary>
public sealed record DashboardDto(
    int StudentCount,
    int AssessmentCount,
    int ClassCount,
    int AssessmentsLast30Days,
    double AchievedShare,
    double InProgressShare,
    double NotAchievedShare,
    IReadOnlyList<ClassSummaryDto> Classes,
    IReadOnlyList<SubjectSummaryDto> Subjects,
    IReadOnlyList<RecentAssessmentDto> Recent,
    IReadOnlyList<AttentionStudentDto> NeedsAttention);

public sealed record ClassSummaryDto(
    string ClassName,
    int StudentCount,
    int AssessmentCount,
    double AchievedShare,
    double InProgressShare,
    double NotAchievedShare);

public sealed record SubjectSummaryDto(
    string SubjectName,
    int AssessmentCount,
    double AchievedShare);

public sealed record RecentAssessmentDto(
    int StudentId,
    string StudentName,
    string SubjectName,
    string? WorkArea,
    Progress Progress,
    DateOnly Date);

/// <summary>Elever med flest ej uppnådda bedömningar, som en mjuk signal till läraren.</summary>
public sealed record AttentionStudentDto(
    int StudentId,
    string StudentName,
    string ClassName,
    int NotAchieved,
    int Total);

// ---------- Skolverket (renskrivet) ----------

public sealed record SubjectDto(string Code, string Name);

/// <summary>Ett ämnes läroplan, renskriven och grupperad för UI.</summary>
public sealed record SyllabusDto(
    string Code,
    string Name,
    IReadOnlyList<CentralContentGroupDto> CentralContents,
    IReadOnlyList<GradingCriterionDto> GradingCriteria);

/// <summary>Punkter under en h4-rubrik, för en årskursspann (t.ex. "4-6").</summary>
public sealed record CentralContentGroupDto(
    string YearSpan,
    string Heading,
    IReadOnlyList<string> Items);

public sealed record GradingCriterionDto(
    int Year,
    string GradeStep,
    string Text);
