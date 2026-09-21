namespace Elevkollen.Shared;

/// <summary>Student progress for an assessment.</summary>
public enum Progress
{
    NotAchieved = 0,
    InProgress = 1,
    Achieved = 2,
}

/// <summary>The domain language for progress, in one place.</summary>
public static class ProgressText
{
    public static readonly string[] GradeSteps = ["A", "B", "C", "D", "E", "F"];

    /// <summary>The grade step for work that does not meet the criterion.</summary>
    public const string NotPassedStep = "F";

    /// <summary>
    /// Progress follows from the grade step: F means the criterion is not met, any other
    /// step that it is. An empty step (years 1 and 3) means acceptable knowledge, which
    /// also counts as met.
    /// </summary>
    public static Progress ProgressFor(string? gradeStep) =>
        gradeStep == NotPassedStep ? Progress.NotAchieved : Progress.Achieved;

    /// <summary>Label for a grade step, including the steps that have no letter.</summary>
    public static string StepLabel(string? gradeStep) => gradeStep switch
    {
        NotPassedStep => "Ej godkänd",
        null or "" => "Godtagbara kunskaper",
        _ => $"Betyg {gradeStep}",
    };

    /// <summary>Progress options in the order they are shown to the teacher.</summary>
    public static readonly Progress[] All =
        [Progress.NotAchieved, Progress.InProgress, Progress.Achieved];

    public static string Label(this Progress progress) => progress switch
    {
        Progress.Achieved => "Uppnått",
        Progress.InProgress => "Pågående",
        _ => "Ej uppnått",
    };

    /// <summary>Short symbol for the tight cells in the class overview.</summary>
    public static string Symbol(this Progress progress) => progress switch
    {
        Progress.Achieved => "✓",
        Progress.InProgress => "~",
        _ => "✕",
    };
}

// ---------- Students ----------

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
/// The class label is written as year + class, e.g. "4B". Teachers enter only the
/// letter, so the year is prefixed when present and not already there.
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
    /// Cleans teacher input: trims, collapses double spaces and capitalises each word.
    /// "andra klassen" becomes "Andra Klassen" and "a" becomes "A".
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

// ---------- Assessments ----------

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
    DateOnly Date,
    int? CriterionYear = null,
    int? CriterionIndex = null);

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
    DateOnly Date,
    int? CriterionYear = null,
    int? CriterionIndex = null);

// ---------- Statistics ----------

/// <summary>Progress over time, per subject, for one student.</summary>
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

// ---------- Class overview ----------

/// <summary>Matrix over a class: students on rows, work areas on columns.</summary>
public sealed record ClassOverviewDto(
    IReadOnlyList<OverviewColumnDto> Columns,
    IReadOnlyList<OverviewRowDto> Rows);

public sealed record OverviewColumnDto(
    string SubjectCode,
    string SubjectName,
    string WorkArea);

/// <summary>Cells index follows Columns. Null means the student has no assessment there.</summary>
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

// ---------- Dashboard ----------

/// <summary>Aggregate of all local data, for the dashboard charts and key figures.</summary>
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

/// <summary>Students with the most unmet assessments, as a soft signal to the teacher.</summary>
public sealed record AttentionStudentDto(
    int StudentId,
    string StudentName,
    string ClassName,
    int NotAchieved,
    int Total);

// ---------- Skolverket (cleaned up) ----------

public sealed record SubjectDto(string Code, string Name);

/// <summary>A subject's syllabus, cleaned up and grouped for the UI.</summary>
public sealed record SyllabusDto(
    string Code,
    string Name,
    IReadOnlyList<CentralContentGroupDto> CentralContents,
    IReadOnlyList<CriterionDto> GradingCriteria);

/// <summary>Items under one h4 heading, for a year span (e.g. "4-6").</summary>
public sealed record CentralContentGroupDto(
    string YearSpan,
    string Heading,
    IReadOnlyList<string> Items);

/// <summary>
/// A single criterion, i.e. one paragraph from Skolverket's grading criteria.
/// Identified by year + paragraph index rather than grade step, since the same
/// paragraph recurs for every step with only the value words swapped.
/// </summary>
public sealed record CriterionDto(
    int Year,
    int Index,
    string GradeStep,
    string Text,
    IReadOnlyList<string> ValueWords)
{
    /// <summary>
    /// The text split so value words can be bolded in the UI, as in LGR.
    /// Computed on access since it is only needed for the criterion being shown.
    /// </summary>
    public IReadOnlyList<TextSegment> Segments => TextSegment.Highlight(Text, ValueWords);
}

/// <summary>
/// A piece of text that is either a value word or plain prose.
/// Lets the UI render bold without going through HTML.
/// </summary>
public sealed record TextSegment(string Text, bool IsValueWord)
{
    /// <summary>
    /// Splits the text at every value word occurrence. The longest word matches first,
    /// so "väl fungerande" is not split by the shorter "fungerande".
    /// With no value word the text is returned as a single plain segment.
    /// </summary>
    public static IReadOnlyList<TextSegment> Highlight(string? text, IReadOnlyList<string>? valueWords)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var words = (valueWords ?? [])
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .OrderByDescending(w => w.Length)
            .ToList();

        if (words.Count == 0)
        {
            return [new TextSegment(text, false)];
        }

        var segments = new List<TextSegment>();
        var plain = 0;
        var i = 0;

        while (i < text.Length)
        {
            var hit = words.FirstOrDefault(w =>
                string.Compare(text, i, w, 0, w.Length, StringComparison.CurrentCultureIgnoreCase) == 0);

            if (hit is null)
            {
                i++;
                continue;
            }

            if (i > plain)
            {
                segments.Add(new TextSegment(text[plain..i], false));
            }

            // Keep the original casing rather than the value word's.
            segments.Add(new TextSegment(text.Substring(i, hit.Length), true));

            i += hit.Length;
            plain = i;
        }

        if (plain < text.Length)
        {
            segments.Add(new TextSegment(text[plain..], false));
        }

        return segments;
    }
}

/// <summary>
/// A criterion with its variants per grade step. The teacher picks the criterion
/// first, then which grade step the student's work corresponds to.
/// </summary>
public sealed record CriterionChoiceDto(
    int Year,
    int Index,
    string Text,
    IReadOnlyList<CriterionDto> Variants)
{
    /// <summary>Display label, e.g. "Årskurs 6 · Kriterium 3".</summary>
    public string Label => $"Årskurs {Year} · Kriterium {Index + 1}";
}

/// <summary>
/// Groups criteria by year and paragraph index.
///
/// Skolverket writes the same number of paragraphs for every grade step, so paragraph n
/// describes the same ability in E, C and A. Should a future syllabus break that
/// parallelism we fall back to letting each grade step be its own choice, so that no
/// content is lost or incorrectly paired.
/// </summary>
public static class CriterionGroup
{
    public static IReadOnlyList<CriterionChoiceDto> Build(IEnumerable<CriterionDto> criteria)
    {
        var choices = new List<CriterionChoiceDto>();

        foreach (var byYear in criteria.GroupBy(c => c.Year).OrderBy(g => g.Key))
        {
            var bySteps = byYear.GroupBy(c => c.GradeStep).ToList();
            var parallel = bySteps.Select(g => g.Count()).Distinct().Count() == 1;

            if (parallel)
            {
                foreach (var byIndex in byYear.GroupBy(c => c.Index).OrderBy(g => g.Key))
                {
                    var variants = byIndex.OrderBy(c => StepOrder(c.GradeStep)).ToList();

                    // The E variant is the most neutral wording and serves as base text.
                    var baseText = variants[0].Text;

                    choices.Add(new CriterionChoiceDto(byYear.Key, byIndex.Key, baseText, variants));
                }
            }
            else
            {
                foreach (var c in byYear.OrderBy(c => StepOrder(c.GradeStep)).ThenBy(c => c.Index))
                {
                    choices.Add(new CriterionChoiceDto(c.Year, c.Index, c.Text, [c]));
                }
            }
        }

        return choices;
    }

    /// <summary>E first, then C, then A. An empty step (years 1 and 3) sorts first.</summary>
    private static int StepOrder(string step) => step switch
    {
        "" => 0,
        "E" => 1,
        "C" => 2,
        "A" => 3,
        _ => 4
    };
}

/// <summary>
/// Maps between school year and Skolverket's year spans, in one place.
/// </summary>
public static class YearSpans
{
    /// <summary>Maps a school year to its span, e.g. 5 to "4-6".</summary>
    public static string? For(int? year) => year switch
    {
        >= 1 and <= 3 => "1-3",
        >= 4 and <= 6 => "4-6",
        >= 7 and <= 9 => "7-9",
        _ => null,
    };

    /// <summary>
    /// Years carrying grading criteria within a span. Criteria land at the end of each
    /// span, but Swedish also has criteria for year 1. Most subjects have none at all
    /// below year 6, since grades are not set before then.
    /// </summary>
    public static int[] CriterionYears(string? span) => span switch
    {
        "1-3" => [1, 3],
        "4-6" => [6],
        "7-9" => [9],
        _ => [],
    };
}

// ---------- Coverage ----------

/// <summary>
/// How much of a subject's central content has been assessed. Computed per class:
/// an item counts as covered as soon as at least one student is assessed against it.
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

/// <summary>The items under one heading, flagged for which are assessed.</summary>
public sealed record CoverageGroupDto(
    string Heading,
    IReadOnlyList<CoverageItemDto> Items)
{
    public int Covered => Items.Count(i => i.IsCovered);
}

public sealed record CoverageItemDto(string Text, bool IsCovered);

/// <summary>
/// Compares the syllabus central content against what the teacher actually assessed.
/// Pure logic without dependencies, so it is easy to unit test.
///
/// Matching is done on the text itself, since an assessment stores no more stable id.
/// If Skolverket rewords an item the link to older assessments therefore breaks — they
/// then end up in <see cref="CoverageDto.Unmatched"/> instead of silently disappearing.
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
    /// Normalises the text before comparison. Skolverket's cleanup can yield different
    /// whitespace over time, and that must not count as a new item.
    /// </summary>
    private static string Key(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
