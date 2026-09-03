# Elevkollen — Copilot Instructions

## Vad appen är
Ett verktyg där lärare dokumenterar elevers prestationer. Ersätter den Excel-fil (`Elevdokumentation.xlsx`) som många lärare använder idag. En lärare lägger upp elever, registrerar en eller flera bedömningar per elev kopplade till Skolverkets centrala innehåll och betygskriterier, och följer utvecklingen över tid.

## Arkitektur

```
Elevkollen.slnx
├── Elevkollen/          Blazor WebAssembly (standalone). All UI + all elevdata i IndexedDB.
└── Elevkollen.Shared/   DTO:er och domänhjälpare. Inga beroenden.
```

Det finns **ingen egen server**. Klienten anropar Skolverkets öppna API direkt från webbläsaren och cachar svaren lokalt i IndexedDB.

### Datakänslighet styr uppdelningen

| Var | Innehåll | Persondata |
|---|---|---|
| IndexedDB-storarna `students` och `assessments` | Elever, bedömningar | **Ja** |
| IndexedDB-storen `meta` | Senaste export, cachad läroplan | Nej |
| Skolverkets API | Läroplanen, hämtas direkt av klienten | Nej |
| `localStorage` | Inloggning, om guiden är sedd | Nej |

**All elevdata stannar på enheten.** Den lagras i webbläsarens IndexedDB och skickas aldrig någonstans. Att det inte finns någon backend är hela poängen med GDPR-minimeringen — lägg aldrig till en.

### Säkerhetskopior
Eftersom datan bara finns i en webbläsare kan läraren exportera den till en `.edok`-fil (`Pages/Backup.razor` → `Services/BackupService.cs` → `wwwroot/js/crypto.js`).

Filformat: `MAGIC "EDOK"(4) | VERSION(1) | SALT(16) | IV(12) | AES-256-GCM-ciphertext`. Nyckeln härleds från användarens lösenord med PBKDF2-SHA256 och 600 000 iterationer. Höj `VERSION` om formatet ändras, och behåll inläsning av äldre versioner. Lagra aldrig lösenordet någonstans — glömt lösenord innebär att kopian är förlorad, och det ska det göra.

En lyckad export eller import skriver `lastExport` i `meta`. `MainLayout` visar en påminnelse när det finns elevdata och det gått mer än `BackupService.ReminderAfterDays` (14) dagar, eller om ingen kopia någonsin tagits.

### Sidor och flöden

| Route | Sida | Syfte |
|---|---|---|
| `/` | `Dashboard.razor` | Startsida: nyckeltal, fördelning, klass- och ämnesdiagram, genvägar |
| `/elever` | `Students.razor` | Lista, sök och filtrera elever |
| `/elever/{id}` | `StudentDetail.razor` | Bedömningar och utveckling för en elev |
| `/elever/{id}/rapport` | `StudentReport.razor` | Utskriftsvänlig sammanställning för utvecklingssamtal |
| `/klassoversikt` | `ClassOverview.razor` | Matris: elever × arbetsområden, färgad efter senaste bedömning |
| `/klassbedomning` | `ClassAssessment.razor` | Bedöm hela klassen i ett svep |
| `/sakerhetskopia` | `Backup.razor` | Krypterad export och import |

**Startsidan** bygger på `StudentStore.GetDashboardAsync()`, som aggregerar all lokal data i ett svep. Diagrammen är ren SVG och CSS (`.dash-*` i `app.css`) i stället för ett diagrambibliotek — färgerna kommer från `--mud-palette-*` så att ljust och mörkt läge följer med.

**Klassbeteckning** skrivs alltid via `ClassLabel.For(schoolYear, className)`. Läraren anger årskurs och klassbokstav var för sig, men överallt i UI:t visas de ihop som t.ex. `4B`. `ClassLabel.Normalize` städar inmatningen till versal begynnelsebokstav. Filtreringen i `StudentStore` matchar på samma sammansatta etikett.

**Klassbedömning** identifierar ett bedömningstillfälle som kombinationen ämne + arbetsområde + datum. När något av dem ändras hämtar `SyncExistingAsync` befintliga poster och förifyller raderna, så att spara uppdaterar i stället för att skapa dubbletter. En förifylld rad som avmarkeras tas bort vid spar. Synkningen är sekvensnumrerad (`_syncToken`) eftersom flera fält kan ändras tätt inpå varandra.

**Rapporten** skrivs ut med `window.print()` via `wwwroot/js/app.js`. Utseendet styrs av `@media print` i `app.css`, som döljer appskalet (`.no-print`, appbar, drawer) och renderar `.report-sheet` svart på vitt. Märk allt som inte hör hemma på papper med `no-print`.

**Introduktionsguiden** (`Layout/TourOverlay.razor` + `Services/TourState.cs`) visas vid första besöket och kan startas om från hjälpikonen i appbaren. Den mäter sitt målelement med `window.tourRect` och ritar fyra `.tour-blur`-paneler runt hålet, så att bara det steget handlar om förblir skarpt. `.tour-shield` täcker hela sidan och gör appen oklickbar så länge guiden är igång.

### Offline
`SyllabusClient` cachar varje lyckat läroplanssvar i `meta`. Vid nätverksfel används den senast hämtade kopian och `ServedFromCache` sätts. Ett trasigt nät får aldrig krascha en sida — fallback är tom lista respektive `null`.

### Köra lokalt
Starta klientprojektet — det är hela applikationen. Läroplanen hämtas direkt från Skolverket via `Syllabus:BaseUrl` i `wwwroot/appsettings.json`.

## Konventioner

- **.NET 10**, `Nullable` och `ImplicitUsings` på i båda projekten.
- **MudBlazor till 100%.** Ingen egen CSS om en Mud-komponent finns. Ingen Bootstrap.
- **Less is more.** Färre filer > fler filer. Lägg relaterad logik tillsammans. Skapa inte ett interface för något som har en enda implementation.
- **Ingen duplicerad domänlogik.** Utvecklingens text, symbol och betygsstegen finns i `ProgressText` (delat) och färgen i `Layout/ProgressUi.cs` (MudBlazor-beroende). Kopiera aldrig tillbaka dem in i en sida.
- **IndexedDB**: all elevdata går via `StudentStore`, som är enda stället som anropar `js/db.js` för elever och bedömningar. Statistik beräknas på klienten. Nya fält läggs till i både JS-modulen och `StudentStore`. Nya stores kräver höjd `DB_VERSION` och en **additiv** `onupgradeneeded` som aldrig rör befintlig data.
- **Aggregering görs i ett svep.** Bygg `ToLookup`/`Dictionary` en gång i stället för att filtrera bedömningslistan inuti en `Select` över elever — datamängden växer med varje termin.
- **DTO:er är `record`s** och bor allihop i `Elevkollen.Shared/Contracts.cs`.
- `sealed` som standard på klasser. Primary constructors där det passar.
- Svenska i UI-text och domänbegrepp, engelska i kod-identifierare.

### Språk
**Allt användarsynligt innehåll är på svenska** — sidor, knappar, dialoger, felmeddelanden och introduktionsguiden. Ingen engelsk UI-text. Kod, kommentarer och commit-meddelanden skrivs också på svenska.

Sortering av namn, klasser och ämnen använder `StringComparer.CurrentCulture` så att å, ä och ö hamnar rätt. Slå därför **inte** på `InvariantGlobalization`.

## Skolverkets API

Bas: `https://api.skolverket.se/syllabus/v1/` (konfigureras i `wwwroot/appsettings.json`).

| Anrop | Ger |
|---|---|
| `GET /subjects?schoolType=GR&timespan=LATEST` | 27 grundskoleämnen, `GRGRMAT01` = Matematik |
| `GET /subjects/{code}?timespan=LATEST` | `centralContents[]` + `knowledgeRequirements[]` |

- `centralContents[]` → `{ text, year }` där `year` är `"1-3"`, `"4-6"` eller `"7-9"`.
- `knowledgeRequirements[]` → `{ text, year, gradeStep }`, `year` = `3`/`6`/`9`, `gradeStep` = `E`/`D`/`C`/`B`/`A`.

### Varför renskrivning behövs
`text` är **HTML** (`<h3>`, `<h4>`, `<ul><li>`, `<strong>`) och innehåller **mjuka bindestreck** (`\u00AD`) som ser ut så här i rådata: `an­vän­ds`. Rakt av i UI blir det oläsligt. `SyllabusTextService` ansvarar för att:

1. Ta bort mjuka bindestreck (`\u00AD`) och `&shy;`.
2. Avkoda HTML-entiteter.
3. Dela upp `<ul><li>`-listor till enskilda valbara punkter.
4. Gruppera punkter under närmast föregående `<h4>`-rubrik.
5. Filtrera bort betygsstegen **D** och **B** — deras text är bara "Elevens kunskaper bedöms sammantaget vara mellan C och E" och de går inte att välja som kriterium.

`SyllabusTextService` är en ren statisk klass utan DI, så den är enkel att enhetstesta.

## Cache busting
Använd .NET 10:s inbyggda fingerprinting — bygg inget eget versionsschema.

- `index.html` refererar `blazor.webassembly#[.{fingerprint}].js`.
- `OverrideHtmlAssetPlaceholders` är satt i `Elevkollen.csproj`.

## Autentisering
Hårdkodat `demo`/`demo` i **en** konstant i `AuthState`. Detta är en platshållare, inte säkerhet — all data ligger ändå lokalt i webbläsaren. Måste bytas mot riktig auth innan hosting.

## Domänordlista

| Svenska | Kod | Betydelse |
|---|---|---|
| Ämne | `Subject` | Matematik, Svenska, ... |
| Centralt innehåll | `CentralContent` | Vad undervisningen ska behandla |
| Betygskriterier | `GradingCriterion` | Krav för ett visst betygssteg |
| Betygssteg | `GradeStep` | A–F |
| Arbetsområde | `WorkArea` | Lärarens eget moment, t.ex. "Bråk och procent" |
| Bedömning | `Assessment` | En elevs prestation vid ett tillfälle |
| Elevens utveckling | `Progress` | Ej uppnått / Pågående / Uppnått |
