using Microsoft.JSInterop;
using Elevkollen.Shared;

namespace Elevkollen.Services;

/// <summary>
/// All elevdata lagras lokalt i webbläsarens IndexedDB och lämnar aldrig enheten.
/// Statistiken beräknas här på klienten.
/// </summary>
public sealed class StudentStore(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _db;

    private async ValueTask<IJSObjectReference> DbAsync() =>
        _db ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/db.js");

    // ---------- Elever ----------

    public async Task<IReadOnlyList<StudentDto>> GetStudentsAsync(string? search = null, string? className = null)
    {
        var db = await DbAsync();
        var students = await db.InvokeAsync<List<StudentRecord>>("getStudents");
        var assessments = await db.InvokeAsync<List<AssessmentRecord>>("getAllAssessments");

        IEnumerable<StudentRecord> query = students;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x =>
                x.Name.Contains(s, StringComparison.OrdinalIgnoreCase)
                || (x.Email?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(className))
        {
            query = query.Where(x => ClassLabel.For(x.SchoolYear, x.ClassName) == className);
        }

        // En lookup byggd en gång, i stället för att söka igenom alla bedömningar per elev.
        var byStudent = assessments.ToLookup(a => a.StudentId);

        return [.. query
            .OrderBy(x => x.Name, StringComparer.CurrentCulture)
            .Select(x =>
            {
                var mine = byStudent[x.Id ?? 0];
                var count = 0;
                DateOnly? last = null;

                foreach (var a in mine)
                {
                    count++;
                    if (last is null || a.Date > last)
                    {
                        last = a.Date;
                    }
                }

                return new StudentDto(
                    x.Id ?? 0, x.Name, x.Email, x.GuardianContact, x.SchoolYear, x.ClassName,
                    count, last);
            })];
    }

    public async Task<IReadOnlyList<string>> GetClassesAsync()
    {
        var db = await DbAsync();
        var students = await db.InvokeAsync<List<StudentRecord>>("getStudents");
        return [.. students
            .Select(x => ClassLabel.For(x.SchoolYear, x.ClassName))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .OrderBy(x => x, StringComparer.CurrentCulture)];
    }

    public async Task<StudentDetailDto?> GetStudentAsync(int id)
    {
        var db = await DbAsync();
        var student = await db.InvokeAsync<StudentRecord?>("getStudent", id);
        if (student is null)
        {
            return null;
        }

        var assessments = await db.InvokeAsync<List<AssessmentRecord>>("getAssessments", id);
        return new StudentDetailDto(
            student.Id ?? 0, student.Name, student.Email, student.GuardianContact,
            student.SchoolYear, student.ClassName,
            [.. assessments.OrderByDescending(a => a.Date).Select(ToDto)]);
    }

    public async Task<int> CreateStudentAsync(SaveStudentRequest req)
    {
        var db = await DbAsync();
        return await db.InvokeAsync<int>("putStudent", new StudentRecord(
            null, req.Name.Trim(), req.Email, req.GuardianContact, req.SchoolYear, req.ClassName));
    }

    public async Task UpdateStudentAsync(int id, SaveStudentRequest req)
    {
        var db = await DbAsync();
        await db.InvokeVoidAsync("putStudent", new StudentRecord(
            id, req.Name.Trim(), req.Email, req.GuardianContact, req.SchoolYear, req.ClassName));
    }

    public async Task DeleteStudentAsync(int id)
    {
        var db = await DbAsync();
        await db.InvokeVoidAsync("deleteStudent", id);
    }

    // ---------- Bedömningar ----------

    public async Task CreateAssessmentAsync(SaveAssessmentRequest req)
    {
        var db = await DbAsync();
        await db.InvokeVoidAsync("putAssessment", FromRequest(null, req));
    }

    /// <summary>
    /// Sparar en blandning av nya och redan sparade bedömningar i en transaktion.
    /// Id som är null skapar en ny post, övriga uppdaterar den befintliga.
    /// </summary>
    public async Task<int> SaveAssessmentsAsync(IReadOnlyList<(int? Id, SaveAssessmentRequest Request)> items)
    {
        if (items.Count == 0)
        {
            return 0;
        }

        var db = await DbAsync();
        var records = items.Select(x => FromRequest(x.Id, x.Request)).ToArray();
        return await db.InvokeAsync<int>("putAssessments", [records]);
    }

    public async Task UpdateAssessmentAsync(int id, SaveAssessmentRequest req)
    {
        var db = await DbAsync();
        await db.InvokeVoidAsync("putAssessment", FromRequest(id, req));
    }

    public async Task DeleteAssessmentAsync(int id)
    {
        var db = await DbAsync();
        await db.InvokeVoidAsync("deleteAssessment", id);
    }

    public async Task DeleteAssessmentsAsync(IReadOnlyList<int> ids)
    {
        if (ids.Count == 0)
        {
            return;
        }

        var db = await DbAsync();
        await db.InvokeVoidAsync("deleteAssessments", [ids.ToArray()]);
    }

    /// <summary>Bedömningar från samma tillfälle: ämne + arbetsområde + datum.</summary>
    public async Task<IReadOnlyList<AssessmentDto>> FindAssessmentsAsync(
        string subjectCode, string? workArea, DateOnly date)
    {
        var db = await DbAsync();
        var items = await db.InvokeAsync<List<AssessmentRecord>>(
            "findAssessments", subjectCode, workArea, date);
        return [.. items.Select(ToDto)];
    }

    // ---------- Klassöversikt ----------

    /// <summary>
    /// Matris för en klass. Kolumnerna är de arbetsområden som faktiskt bedömts,
    /// och varje cell visar elevens senaste utveckling inom området.
    /// </summary>
    public async Task<ClassOverviewDto> GetClassOverviewAsync(string? className, string? subjectCode = null)
    {
        var db = await DbAsync();
        var students = await db.InvokeAsync<List<StudentRecord>>("getStudents");
        var assessments = await db.InvokeAsync<List<AssessmentRecord>>("getAllAssessments");

        var included = students
            .Where(s => string.IsNullOrWhiteSpace(className)
                || ClassLabel.For(s.SchoolYear, s.ClassName) == className)
            .OrderBy(s => s.Name, StringComparer.CurrentCulture)
            .ToArray();

        var ids = included.Select(s => s.Id ?? 0).ToHashSet();

        var relevant = assessments
            .Where(a => ids.Contains(a.StudentId))
            .Where(a => string.IsNullOrWhiteSpace(subjectCode) || a.SubjectCode == subjectCode)
            .ToArray();

        var columns = relevant
            .GroupBy(a => (a.SubjectCode, a.SubjectName, Area: a.WorkArea ?? "Övrigt"))
            .Select(g => new
            {
                Column = new OverviewColumnDto(g.Key.SubjectCode, g.Key.SubjectName, g.Key.Area),
                Latest = g.Max(a => a.Date),
            })
            .OrderBy(x => x.Column.SubjectName, StringComparer.CurrentCulture)
            .ThenByDescending(x => x.Latest)
            .Select(x => x.Column)
            .ToArray();

        // Bara senaste bedömningen och antalet behövs per cell, så vi slipper sortera varje grupp.
        var byKey = relevant
            .GroupBy(a => (a.StudentId, a.SubjectCode, Area: a.WorkArea ?? "Övrigt"))
            .ToDictionary(
                g => g.Key,
                g => (Latest: g.MaxBy(a => a.Date)!, Count: g.Count()));

        var rows = included
            .Select(s => new OverviewRowDto(
                s.Id ?? 0, s.Name, s.ClassName, s.SchoolYear,
                [.. columns.Select(c =>
                    byKey.TryGetValue((s.Id ?? 0, c.SubjectCode, c.WorkArea), out var hit)
                        ? new OverviewCellDto(hit.Latest.Progress, hit.Latest.GradeStep, hit.Latest.Date, hit.Count)
                        : null)]))
            .ToArray();

        return new ClassOverviewDto(columns, rows);
    }

    /// <summary>
    /// Ämnen som faktiskt har bedömts, för filterlistor. Betydligt billigare än att
    /// bygga hela klassmatrisen bara för att få fram namnen.
    /// </summary>
    public async Task<IReadOnlyList<SubjectDto>> GetAssessedSubjectsAsync()
    {
        var db = await DbAsync();
        var assessments = await db.InvokeAsync<List<AssessmentRecord>>("getAllAssessments");

        return [.. assessments
            .DistinctBy(a => a.SubjectCode)
            .Select(a => new SubjectDto(a.SubjectCode, a.SubjectName))
            .OrderBy(s => s.Name, StringComparer.CurrentCulture)];
    }

    /// <summary>
    /// De centrala innehåll som faktiskt bedömts i ett ämne, för täckningsgraden.
    /// Filtreras på klass när en sådan anges. Returnerar bara distinkta texter — vem
    /// som bedömts spelar ingen roll, en punkt räknas som täckt vid första bedömningen.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetAssessedContentsAsync(string subjectCode, string? className = null)
    {
        var db = await DbAsync();
        var assessments = await db.InvokeAsync<List<AssessmentRecord>>("getAllAssessments");

        IEnumerable<AssessmentRecord> query = assessments.Where(a => a.SubjectCode == subjectCode);

        if (!string.IsNullOrWhiteSpace(className))
        {
            var students = await db.InvokeAsync<List<StudentRecord>>("getStudents");

            var ids = students
                .Where(s => ClassLabel.For(s.SchoolYear, s.ClassName) == className)
                .Select(s => s.Id ?? 0)
                .ToHashSet();

            query = query.Where(a => ids.Contains(a.StudentId));
        }

        return [.. query
            .Select(a => a.CentralContent)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct(StringComparer.Ordinal)];
    }

    // ---------- Startsida ----------
    /// <summary>Sammanställning för startsidan: nyckeltal, fördelning per klass och ämne.</summary>
    public async Task<DashboardDto> GetDashboardAsync()
    {
        var db = await DbAsync();
        var students = await db.InvokeAsync<List<StudentRecord>>("getStudents");
        var assessments = await db.InvokeAsync<List<AssessmentRecord>>("getAllAssessments");

        var byStudent = students.ToDictionary(s => s.Id ?? 0);
        var byStudentId = assessments.ToLookup(a => a.StudentId);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var since = today.AddDays(-30);

        var classes = students
            .GroupBy(s => ClassLabel.For(s.SchoolYear, s.ClassName))
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .Select(g =>
            {
                var mine = g.SelectMany(s => byStudentId[s.Id ?? 0]).ToArray();
                return new ClassSummaryDto(
                    g.Key, g.Count(), mine.Length,
                    Share(mine, Progress.Achieved),
                    Share(mine, Progress.InProgress),
                    Share(mine, Progress.NotAchieved));
            })
            .OrderBy(c => c.ClassName, StringComparer.CurrentCulture)
            .ToArray();

        var subjects = assessments
            .GroupBy(a => a.SubjectName)
            .Select(g => new SubjectSummaryDto(g.Key, g.Count(), Share([.. g], Progress.Achieved)))
            .OrderByDescending(s => s.AssessmentCount)
            .Take(6)
            .ToArray();

        var recent = assessments
            .OrderByDescending(a => a.Date)
            .ThenByDescending(a => a.Id)
            .Take(6)
            .Select(a => new RecentAssessmentDto(
                a.StudentId,
                byStudent.TryGetValue(a.StudentId, out var s) ? s.Name : "Okänd elev",
                a.SubjectName, a.WorkArea, a.Progress, a.Date))
            .ToArray();

        var needsAttention = students
            .Select(s =>
            {
                var notAchieved = 0;
                var total = 0;

                foreach (var a in byStudentId[s.Id ?? 0])
                {
                    total++;
                    if (a.Progress == Progress.NotAchieved)
                    {
                        notAchieved++;
                    }
                }

                return new AttentionStudentDto(
                    s.Id ?? 0, s.Name, ClassLabel.For(s.SchoolYear, s.ClassName),
                    notAchieved, total);
            })
            .Where(x => x.NotAchieved > 0)
            .OrderByDescending(x => x.NotAchieved)
            .Take(5)
            .ToArray();

        return new DashboardDto(
            students.Count,
            assessments.Count,
            classes.Length,
            assessments.Count(a => a.Date >= since && a.Date <= today),
            Share(assessments, Progress.Achieved),
            Share(assessments, Progress.InProgress),
            Share(assessments, Progress.NotAchieved),
            classes, subjects, recent, needsAttention);
    }

    private static double Share(IReadOnlyCollection<AssessmentRecord> items, Progress progress) =>
        items.Count == 0 ? 0 : 100.0 * items.Count(a => a.Progress == progress) / items.Count;

    // ---------- Statistik ----------

    public async Task<StudentStatsDto?> GetStatsAsync(int id)
    {
        var db = await DbAsync();
        var items = await db.InvokeAsync<List<AssessmentRecord>>("getAssessments", id);
        var ordered = items.OrderBy(a => a.Date).ToArray();

        var subjects = ordered
            .GroupBy(a => (a.SubjectCode, a.SubjectName))
            .Select(g => new SubjectProgressDto(
                g.Key.SubjectCode,
                g.Key.SubjectName,
                g.Count(),
                g.Count(a => a.Progress == Progress.Achieved),
                g.Count(a => a.Progress == Progress.InProgress),
                g.Count(a => a.Progress == Progress.NotAchieved),
                // ordered är redan stigande på datum, så sista träffen är den senaste.
                g.LastOrDefault(a => a.GradeStep is not null)?.GradeStep))
            .OrderBy(s => s.SubjectName, StringComparer.CurrentCulture)
            .ToArray();

        var timeline = ordered
            .Select(a => new ProgressPointDto(a.Date, a.SubjectName, a.Progress, a.GradeStep))
            .ToArray();

        return new StudentStatsDto(id, subjects, timeline);
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            try
            {
                await _db.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Sidan är redan stängd — inget att städa.
            }
        }
    }

    private static AssessmentDto ToDto(AssessmentRecord a) => new(
        a.Id ?? 0, a.StudentId, a.SubjectCode, a.SubjectName, a.WorkArea, a.CentralContent,
        a.GradingCriterion, a.GradeStep, a.Progress, a.Comment, a.Date);

    private static AssessmentRecord FromRequest(int? id, SaveAssessmentRequest r) => new(
        id, r.StudentId, r.SubjectCode, r.SubjectName, r.WorkArea, r.CentralContent,
        r.GradingCriterion, r.GradeStep, r.Progress, r.Comment, r.Date);

    /// <summary>Id är null vid nyskapande så att IndexedDB tilldelar nyckeln.</summary>
    private sealed record StudentRecord(
        int? Id,
        string Name,
        string? Email,
        string? GuardianContact,
        int? SchoolYear,
        string? ClassName);

    private sealed record AssessmentRecord(
        int? Id,
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
}
