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

// ---------- Täckningsgrad ----------

/// <summary>
/// Hur stor del av ett ämnes centrala innehåll som blivit bedömt. Beräknas per klass:
/// en punkt räknas som täckt så snart minst en elev bedömts mot den.
/// </summary>
public sealed record CoverageDto(
    string SubjectName,
    string YearSpan,
    int Covered,
    int Total,
    IReadOnlyList<CoverageGroupDto> Groups,
    IReadOnlyList<string> Unmatched)
{
    public double Share => Total == 0 ? 0 : (double)Covered / Total;
}

/// <summary>Punkterna under en rubrik, med markering för vilka som är bedömda.</summary>
public sealed record CoverageGroupDto(
    string Heading,
    IReadOnlyList<CoverageItemDto> Items)
{
    public int Covered => Items.Count(i => i.IsCovered);
}

public sealed record CoverageItemDto(string Text, bool IsCovered);

/// <summary>
/// Jämför läroplanens centrala innehåll mot det läraren faktiskt bedömt. Ren logik utan
/// beroenden, så den är enkel att enhetstesta.
///
/// Kopplingen görs på texten själv, eftersom en bedömning inte lagrar något stabilare id.
/// Omformulerar Skolverket en punkt bryts därför kopplingen till äldre bedömningar — de
/// hamnar då i <see cref="CoverageDto.Unmatched"/> i stället för att tyst försvinna.
/// </summary>
public static class CoverageCalculator
{
    public static CoverageDto For(
        SyllabusDto syllabus,
        string yearSpan,
        IEnumerable<string> assessedContents)
    {
        var assessed = assessedContents
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(Key)
            .ToHashSet(StringComparer.Ordinal);

        var groups = syllabus.CentralContents
            .Where(g => g.YearSpan == yearSpan)
            .Select(g => new CoverageGroupDto(
                g.Heading,
                [.. g.Items.Distinct(StringComparer.Ordinal)
                    .Select(i => new CoverageItemDto(i, assessed.Contains(Key(i))))]))
            .Where(g => g.Items.Count > 0)
            .ToArray();

        var known = groups
            .SelectMany(g => g.Items)
            .Select(i => Key(i.Text))
            .ToHashSet(StringComparer.Ordinal);

        var unmatched = assessedContents
            .Where(c => !string.IsNullOrWhiteSpace(c) && !known.Contains(Key(c)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.CurrentCulture)
            .ToArray();

        var items = groups.SelectMany(g => g.Items).ToArray();

        return new CoverageDto(
            syllabus.Name,
            yearSpan,
            items.Count(i => i.IsCovered),
            items.Length,
            groups,
            unmatched);
    }

    /// <summary>
    /// Normaliserar texten inför jämförelse. Skolverkets renskrivning kan ge olika
    /// mellanrum över tid, och det ska inte räknas som en ny punkt.
    /// </summary>
    private static string Key(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
