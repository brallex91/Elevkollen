# Elevkollen

A tool where teachers document student performance against the Swedish National Agency for Education's central content and grading criteria.

**All student data stays in the teacher's browser.** There is no server, no database and no cloud sign-in. That is not a limitation — it is the whole point.

---

## Why the app looks the way it does

A teacher handles names, grades and judgements about children. That is sensitive personal data. The simplest path to GDPR compliance is to never collect it centrally.

The application is therefore a pure Blazor WebAssembly client. Students and assessments are stored in the browser's IndexedDB, the syllabus is fetched directly from Skolverket's open API, and nothing passes through a backend — because there isn't one.

| Where | Contents | Personal data |
|---|---|---|
| IndexedDB `students`, `assessments` | Students, assessments | **Yes** |
| IndexedDB `meta` | Last export, cached syllabus | No |
| Skolverket's API | The syllabus | No |
| `localStorage` | Sign-in, whether the tour has been seen | No |

The trade-off is that the data is as volatile as the browser profile. Backups are therefore a first-class feature, not an afterthought.

---

## Features

| Page | Route | What it does |
|---|---|---|
| Dashboard | `/` | Key figures, grade distribution, class and subject charts |
| Students | `/elever` | List, search and filter |
| Student card | `/elever/{id}` | Assessments and progress over time |
| Report | `/elever/{id}/rapport` | Print-friendly basis for parent-teacher meetings |
| Class overview | `/klassoversikt` | Matrix of students × work areas, colored by the latest assessment |
| Class assessment | `/klassbedomning` | Assess a whole class in one pass |
| Backup | `/sakerhetskopia` | Encrypted export and import |

**Class assessment** identifies an occasion as subject + work area + date. When any of them changes, existing records are fetched and prefill the rows, so saving updates them instead of creating duplicates.

**The report** is printed via `window.print()`. The app shell is hidden by `@media print` rules so the paper contains only the student's summary.

**The charts** are plain SVG and CSS rather than a charting library. The colors come from MudBlazor's palette variables, so light and dark mode follow along without extra code.

---

## Backups

Since the data only exists in one browser, the teacher can export it to an encrypted `.edok` file.

```
MAGIC "EDOK"(4) | VERSION(1) | SALT(16) | IV(12) | AES-256-GCM ciphertext
```

The key is derived from the teacher's password using PBKDF2-SHA256 with 600,000 iterations. The password is never stored anywhere. **A forgotten password means the backup is lost** — and it should, otherwise the encryption would be theatre.

The app reminds the user to take a fresh backup after more than 14 days, or if no backup has ever been made.

---

## Skolverket's API

Base: `https://api.skolverket.se/syllabus/v1/`, configured in `wwwroot/appsettings.json`.

| Call | Returns |
|---|---|
| `GET /subjects?schoolType=GR&timespan=LATEST` | 27 compulsory school subjects |
| `GET /subjects/{code}?timespan=LATEST` | Central content and grading criteria |

The texts arrive as HTML with soft hyphens (`\u00AD`) baked in, which looks like `an­vän­ds` in the raw data and is unreadable if rendered as-is. `SyllabusTextService` cleans it up: it strips soft hyphens, decodes entities, splits lists into selectable items, groups them under the nearest heading, and filters out grade steps D and B — their text only states that the knowledge lies between two other steps, so they cannot be picked as a criterion.

Every successful response is cached in IndexedDB. On a network failure the most recent copy is used. A broken connection must never crash a page.

---

## Running locally

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone <repo-url>
cd Elevkollen
dotnet run --project Elevkollen.csproj
```

The client project is the entire application. The syllabus is fetched directly from Skolverket, so an internet connection is needed on the first run.

Demo sign-in is `demo` / `demo`.

---

## Deployment

The project is published automatically to GitHub Pages via `.github/workflows/deploy.yml` on every push to `main`.

Three things are required for a Blazor WebAssembly app to work on Pages, and the workflow handles all of them:

1. `<base href>` is rewritten to the repository subdirectory, since the site lives at `https://<user>.github.io/<repo>/` and not at the root.
2. `.nojekyll` is added, otherwise Jekyll filters out `_framework/` — folders starting with an underscore are ignored by default, and that is where the entire .NET runtime lives.
3. `index.html` is copied to `404.html`, so a direct link to `/elever/3` lands in the client router instead of an error page.

Enable Pages under **Settings → Pages → Source: GitHub Actions**.

`.github/workflows/build.yml` builds every pull request without deploying.

---

## Architecture

```
Elevkollen.slnx
├── Elevkollen/          Blazor WebAssembly. All UI and all student data.
└── Elevkollen.Shared/   DTOs and domain helpers. No dependencies.
```

**Conventions**

- .NET 10, nullable and implicit usings enabled.
- MudBlazor all the way. No custom CSS where a Mud component is enough.
- No duplicated domain logic. Progress text and grade steps live in `ProgressText`, the color in `ProgressUi`.
- All student data goes through `StudentStore`, the only place that talks to `js/db.js`.
- New IndexedDB stores require a bumped `DB_VERSION` and an **additive** `onupgradeneeded` that never touches existing data.
- Aggregation is done in a single pass with `ToLookup`/`Dictionary` — the dataset grows every term.
- Code, comments, documentation and commit messages are in English. Only user-facing UI text is Swedish, since the users are Swedish teachers.

Sorting uses `StringComparer.CurrentCulture` so that å, ä and ö end up in the right place. Do **not** enable `InvariantGlobalization`.

---

## Before production use

The sign-in is a hard-coded placeholder in `AuthState`, not security. It does not hold up for public hosting with real student data and must be replaced with real authentication first.

---

## License

MIT. See [LICENSE](LICENSE).
