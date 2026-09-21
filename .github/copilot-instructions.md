# Elevkollen — Copilot Instructions

## What the app is
A tool where teachers document student performance. It replaces the Excel file (`Elevdokumentation.xlsx`) many teachers use today. A teacher adds students, records one or more assessments per student tied to Skolverket's central content and grading criteria, and follows the progress over time.

## Architecture

```
Elevkollen.slnx
├── Elevkollen/          Blazor WebAssembly (standalone). All UI + all student data in IndexedDB.
└── Elevkollen.Shared/   DTOs and domain helpers. No dependencies.
```

There is **no server of our own**. The client calls Skolverket's open API directly from the browser and caches the responses locally in IndexedDB.

### Data sensitivity drives the split

| Where | Contents | Personal data |
|---|---|---|
| IndexedDB stores `students` and `assessments` | Students, assessments | **Yes** |
| IndexedDB store `meta` | Last export, cached syllabus | No |
| Skolverket's API | The syllabus, fetched directly by the client | No |
| `localStorage` | Sign-in, whether the tour has been seen | No |

**All student data stays on the device.** It is stored in the browser's IndexedDB and never sent anywhere. Having no backend is the entire point of the GDPR minimization — never add one.

### Backups
Since the data only exists in one browser, the teacher can export it to an `.edok` file (`Pages/Backup.razor` → `Services/BackupService.cs` → `wwwroot/js/crypto.js`).

File format: `MAGIC "EDOK"(4) | VERSION(1) | SALT(16) | IV(12) | AES-256-GCM ciphertext`. The key is derived from the user's password with PBKDF2-SHA256 and 600,000 iterations. Bump `VERSION` if the format changes, and keep reading older versions. Never store the password anywhere — a forgotten password means the backup is lost, and it should.

A successful export or import writes `lastExport` to `meta`. `MainLayout` shows a reminder when student data exists and more than `BackupService.ReminderAfterDays` (14) days have passed, or if no backup has ever been made.

### Pages and flows

| Route | Page | Purpose |
|---|---|---|
| `/` | `Dashboard.razor` | Dashboard: key figures, distribution, class and subject charts, shortcuts |
| `/elever` | `Students.razor` | List, search and filter students |
| `/elever/{id}` | `StudentDetail.razor` | Assessments and progress for one student |
| `/elever/{id}/rapport` | `StudentReport.razor` | Print-friendly summary for parent-teacher meetings |
| `/klassoversikt` | `ClassOverview.razor` | Matrix: students × work areas, colored by the latest assessment |
| `/klassbedomning` | `ClassAssessment.razor` | Assess a whole class in one pass |
| `/sakerhetskopia` | `Backup.razor` | Encrypted export and import |

**The dashboard** is built on `StudentStore.GetDashboardAsync()`, which aggregates all local data in a single pass. The charts are plain SVG and CSS (`.dash-*` in `app.css`) rather than a charting library — the colors come from `--mud-palette-*` so light and dark mode follow along.

**Class labels** are always rendered via `ClassLabel.For(schoolYear, className)`. The teacher enters the school year and class letter separately, but everywhere in the UI they are shown together, e.g. `4B`. `ClassLabel.Normalize` cleans the input to an uppercase first letter. Filtering in `StudentStore` matches on the same composed label.

**Class assessment** identifies an occasion as the combination subject + work area + date. When any of them changes, `SyncExistingAsync` fetches existing records and prefills the rows, so saving updates instead of creating duplicates. A prefilled row that is unchecked gets deleted on save. The sync is sequence-numbered (`_syncToken`) because several fields can change in quick succession.

**The report** is printed with `window.print()` via `wwwroot/js/app.js`. The appearance is controlled by `@media print` in `app.css`, which hides the app shell (`.no-print`, appbar, drawer) and renders `.report-sheet` black on white. Mark anything that does not belong on paper with `no-print`.

**The intro tour** (`Layout/TourOverlay.razor` + `Services/TourState.cs`) shows on the first visit and can be restarted from the help icon in the appbar. It measures its target element with `window.tourRect` and draws four `.tour-blur` panels around the hole, so only what the step is about stays sharp. `.tour-shield` covers the whole page and makes the app unclickable while the tour is running.

### Offline
`SyllabusClient` caches every successful syllabus response in `meta`. On a network failure the most recently fetched copy is used and `ServedFromCache` is set. A broken connection must never crash a page — the fallback is an empty list or `null` respectively.

### Running locally
Start the client project — it is the entire application. The syllabus is fetched directly from Skolverket via `Syllabus:BaseUrl` in `wwwroot/appsettings.json`.

## Conventions

- **.NET 10**, `Nullable` and `ImplicitUsings` enabled in both projects.
- **MudBlazor all the way.** No custom CSS if a Mud component exists. No Bootstrap.
- **Less is more.** Fewer files > more files. Keep related logic together. Do not create an interface for something with a single implementation.
- **No duplicated domain logic.** The progress text, symbol and grade steps live in `ProgressText` (shared) and the color in `Layout/ProgressUi.cs` (MudBlazor-dependent). Never copy them back into a page.
- **Year spans** are mapped in one place: `YearSpans` in `Elevkollen.Shared/Contracts.cs`. Never re-derive span or criterion-year logic in a component.
- **IndexedDB**: all student data goes through `StudentStore`, the only place that calls `js/db.js` for students and assessments. Statistics are computed on the client. New fields are added in both the JS module and `StudentStore`. New stores require a bumped `DB_VERSION` and an **additive** `onupgradeneeded` that never touches existing data.
- **Aggregation is done in a single pass.** Build a `ToLookup`/`Dictionary` once instead of filtering the assessment list inside a `Select` over students — the dataset grows every term.
- **DTOs are `record`s** and all live in `Elevkollen.Shared/Contracts.cs`.
- `sealed` by default on classes. Primary constructors where they fit.

### Language
**Everything except user-facing UI text is in English** — code identifiers, comments, XML docs, documentation and commit messages. Do not write Swedish comments.

**All user-visible content is in Swedish** — pages, buttons, dialogs, error messages and the intro tour, since the users are Swedish teachers. No English UI text. Routes and domain terms shown to the user stay Swedish as well.

Sorting of names, classes and subjects uses `StringComparer.CurrentCulture` so that å, ä and ö end up in the right place. Therefore do **not** enable `InvariantGlobalization`.

## Skolverket's API

Base: `https://api.skolverket.se/syllabus/v1/` (configured in `wwwroot/appsettings.json`).

| Call | Returns |
|---|---|
| `GET /subjects?schoolType=GR&timespan=LATEST` | 27 compulsory school subjects, `GRGRMAT01` = Matematik |
| `GET /subjects/{code}?timespan=LATEST` | `centralContents[]` + `knowledgeRequirements[]` |

- `centralContents[]` → `{ text, year }` where `year` is `"1-3"`, `"4-6"` or `"7-9"`.
- `knowledgeRequirements[]` → `{ text, year, gradeStep }`, `year` = `1`/`3`/`6`/`9`, `gradeStep` = `E`/`D`/`C`/`B`/`A`.

Note that criteria are not published for every year in every subject. A `1-3` span must therefore accept both year `1` and year `3`; `YearSpans.CriterionYears` owns that rule.

### Why cleanup is needed
`text` is **HTML** (`<h3>`, `<h4>`, `<ul><li>`, `<strong>`) and contains **soft hyphens** (`\u00AD`) that look like this in the raw data: `an­van­ds`. Rendered as-is in the UI it is unreadable. `SyllabusTextService` is responsible for:

1. Stripping soft hyphens (`\u00AD`) and `&shy;`.
2. Decoding HTML entities.
3. Splitting `<ul><li>` lists into individual selectable items.
4. Grouping items under the nearest preceding `<h4>` heading.
5. Filtering out grade steps **D** and **B** — their text only says the student's knowledge is judged overall to be between two other steps, so they cannot be picked as a criterion.
6. Preserving the value words in `<strong>` as bold segments, since they are the only thing that distinguishes one grade step from another.

`SyllabusTextService` is a plain static class without DI, so it is easy to unit test.

## Cache busting
Use .NET 10's built-in fingerprinting — do not build a custom versioning scheme.

- `index.html` references `blazor.webassembly#[.{fingerprint}].js`.
- `OverrideHtmlAssetPlaceholders` is set in `Elevkollen.csproj`.

## Authentication
Hard-coded `demo`/`demo` in **one** constant in `AuthState`. This is a placeholder, not security — all data lives locally in the browser anyway. It must be replaced with real auth before hosting.

## Domain glossary

| Swedish (UI) | Code | Meaning |
|---|---|---|
| Ämne | `Subject` | Matematik, Svenska, ... |
| Centralt innehåll | `CentralContent` | What the teaching must cover |
| Betygskriterier | `GradingCriterion` | Requirements for a given grade step |
| Betygssteg | `GradeStep` | A–F |
| Arbetsområde | `WorkArea` | The teacher's own unit, e.g. "Bråk och procent" |
| Bedömning | `Assessment` | A student's performance on one occasion |
| Elevens utveckling | `Progress` | Ej uppnått / Pågående / Uppnått |
| Årskursspann | `YearSpan` | `1-3`, `4-6`, `7-9` |
