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

    /// <summary>Betygssteget för en prestation som inte når kriteriet.</summary>
    public const string NotPassedStep = "F";

    /// <summary>
    /// Utvecklingen följer av betygssteget: F betyder att kriteriet inte är uppnått,
    /// övriga steg att det är uppnått. Tomt steg (årskurs 1 och 3) innebär
    /// godtagbara kunskaper, vilket också räknas som uppnått.
    /// </summary>
    public static Progress ProgressFor(string? gradeStep) =>
        gradeStep == NotPassedStep ? Progress.NotAchieved : Progress.Achieved;

    /// <summary>Etikett för ett betygssteg, inklusive de steg som saknar bokstav.</summary>
    public static string StepLabel(string? gradeStep) => gradeStep switch
    {
        NotPassedStep => "Ej godkänd",
        null or "" => "Godtagbara kunskaper",
        _ => $"Betyg {gradeStep}",
    };

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
    IReadOnlyList<CriterionDto> GradingCriteria);

/// <summary>Punkter under en h4-rubrik, för en årskursspann (t.ex. "4-6").</summary>
public sealed record CentralContentGroupDto(
    string YearSpan,
    string Heading,
    IReadOnlyList<string> Items);

/// <summary>
/// Ett enskilt kriterium, dvs. ett stycke ur Skolverkets betygskriterier.
/// Identifieras av årskurs + styckeindex, inte av betygssteg, eftersom samma
/// stycke återkommer i varje betygssteg med bara värdeorden utbytta.
/// </summary>
public sealed record CriterionDto(
    int Year,
    int Index,
    string GradeStep,
    string Text,
    IReadOnlyList<string> ValueWords)
{
    /// <summary>
    /// Texten uppdelad så att värdeorden kan fetmarkeras i UI, precis som i LGR.
    /// Beräknas vid anrop eftersom den bara behövs för det kriterium som visas.
    /// </summary>
    public IReadOnlyList<TextSegment> Segments => TextSegment.Highlight(Text, ValueWords);
}

/// <summary>
/// En bit text som antingen är ett värdeord eller vanlig löptext.
/// Låter UI:t rendera fetstil utan att gå via HTML.
/// </summary>
public sealed record TextSegment(string Text, bool IsValueWord)
{
    /// <summary>
    /// Delar texten vid varje förekomst av ett värdeord. Längsta ordet matchas
    /// först, så att "väl fungerande" inte delas av kortare "fungerande".
    /// Hittas inget värdeord returneras texten som ett enda vanligt segment.
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

            // Behåll originalets skiftläge i stället för värdeordets.
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
/// Ett kriterium med sina varianter per betygssteg. Läraren väljer först kriteriet
/// och sedan vilket betygssteg elevens prestation motsvarar.
/// </summary>
public sealed record CriterionChoiceDto(
    int Year,
    int Index,
    string Text,
    IReadOnlyList<CriterionDto> Variants)
{
    /// <summary>Visningsetikett, t.ex. "Årskurs 6 · Kriterium 3".</summary>
    public string Label => $"Årskurs {Year} · Kriterium {Index + 1}";
}

/// <summary>
/// Grupperar kriterier per årskurs och styckeindex.
///
/// Skolverket skriver samma antal stycken för varje betygssteg, så stycke n
/// beskriver samma förmåga i E, C och A. Skulle en framtida läroplan bryta den
/// parallelliteten faller vi tillbaka på att låta varje betygssteg bli ett eget
/// val, så att inget innehåll tappas bort eller paras ihop felaktigt.
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

                    // E-varianten är den mest neutrala formuleringen och används som bastext.
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

    /// <summary>E först, sedan C, sedan A. Tomt steg (årskurs 1/3) hamnar först.</summary>
    private static int StepOrder(string step) => step switch
    {
        "" => 0,
        "E" => 1,
        "C" => 2,
        "A" => 3,
        _ => 4
    };
}

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
