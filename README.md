# Elevkollen

Ett verktyg där lärare dokumenterar elevers prestationer mot Skolverkets centrala innehåll och betygskriterier. Byggt för att ersätta den `Elevdokumentation.xlsx` som många lärare håller igång manuellt idag.

**All elevdata stannar i lärarens webbläsare.** Det finns ingen server, ingen databas och ingen inloggning mot något moln. Det är inte en begränsning — det är hela poängen.

---

## Varför appen ser ut som den gör

En lärare hanterar namn, betyg och omdömen om barn. Det är känsliga personuppgifter. Den enklaste vägen till GDPR-efterlevnad är att aldrig samla in dem centralt.

Därför är applikationen en ren Blazor WebAssembly-klient. Elever och bedömningar lagras i webbläsarens IndexedDB, läroplanen hämtas direkt från Skolverkets öppna API, och ingenting passerar en backend — för det finns ingen.

| Var | Innehåll | Persondata |
|---|---|---|
| IndexedDB `students`, `assessments` | Elever, bedömningar | **Ja** |
| IndexedDB `meta` | Senaste export, cachad läroplan | Nej |
| Skolverkets API | Läroplanen | Nej |
| `localStorage` | Inloggning, om guiden är sedd | Nej |

Kompromissen är att datan är lika flyktig som webbläsarprofilen. Därför är säkerhetskopiering en förstklassig funktion och inte en eftertanke.

---

## Funktioner

| Sida | Route | Vad den gör |
|---|---|---|
| Startsida | `/` | Nyckeltal, betygsfördelning, klass- och ämnesdiagram |
| Elever | `/elever` | Lista, sök och filtrera |
| Elevkort | `/elever/{id}` | Bedömningar och utveckling över tid |
| Rapport | `/elever/{id}/rapport` | Utskriftsvänligt underlag för utvecklingssamtal |
| Klassöversikt | `/klassoversikt` | Matris elever × arbetsområden, färgad efter senaste bedömning |
| Klassbedömning | `/klassbedomning` | Bedöm en hel klass i ett svep |
| Säkerhetskopia | `/sakerhetskopia` | Krypterad export och import |

**Klassbedömning** identifierar ett tillfälle som ämne + arbetsområde + datum. Ändras något av dem hämtas befintliga poster och förifyller raderna, så att spara uppdaterar i stället för att skapa dubbletter.

**Rapporten** skrivs ut via `window.print()`. Appskalet döljs av `@media print`-regler så att pappret bara innehåller elevens sammanställning.

**Diagrammen** är ren SVG och CSS i stället för ett diagrambibliotek. Färgerna kommer från MudBlazors palettvariabler, så ljust och mörkt läge följer med utan extra kod.

---

## Säkerhetskopior

Eftersom datan bara finns i en webbläsare kan läraren exportera den till en krypterad `.edok`-fil.

```
MAGIC "EDOK"(4) | VERSION(1) | SALT(16) | IV(12) | AES-256-GCM-ciphertext
```

Nyckeln härleds från lärarens lösenord med PBKDF2-SHA256 och 600 000 iterationer. Lösenordet lagras aldrig någonstans. **Glömt lösenord innebär att kopian är förlorad** — och det ska det göra, annars vore krypteringen teater.

Appen påminner om att ta en ny kopia när det gått mer än 14 dagar, eller om ingen kopia någonsin tagits.

---

## Skolverkets API

Bas: `https://api.skolverket.se/syllabus/v1/`, konfigurerad i `wwwroot/appsettings.json`.

| Anrop | Ger |
|---|---|
| `GET /subjects?schoolType=GR&timespan=LATEST` | 27 grundskoleämnen |
| `GET /subjects/{code}?timespan=LATEST` | Centralt innehåll och betygskriterier |

Texterna kommer som HTML med mjuka bindestreck (`\u00AD`) inbakade, vilket ser ut som `an­vän­ds` i rådata och blir oläsligt rakt av i UI. `SyllabusTextService` renskriver: tar bort mjuka bindestreck, avkodar entiteter, delar upp listor till valbara punkter, grupperar dem under närmaste rubrik och filtrerar bort betygsstegen D och B — deras text säger bara att kunskaperna ligger mellan två andra steg, så de går inte att välja som kriterium.

Varje lyckat svar cachas i IndexedDB. Vid nätverksfel används den senaste kopian. Ett trasigt nät får aldrig krascha en sida.

---

## Köra lokalt

Kräver [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone <repo-url>
cd Elevkollen
dotnet run --project Elevkollen.csproj
```

Klientprojektet är hela applikationen. Läroplanen hämtas direkt från Skolverket, så en internetanslutning behövs vid första körningen.

Inloggning i demoläget är `demo` / `demo`.

---

## Deployment

Projektet publiceras automatiskt till GitHub Pages via `.github/workflows/deploy.yml` vid push till `main`.

Tre saker krävs för att en Blazor WebAssembly-app ska fungera på Pages, och workflowen sköter alla:

1. `<base href>` skrivs om till repots underkatalog, eftersom sidan ligger på `https://<användare>.github.io/<repo>/` och inte i roten.
2. `.nojekyll` läggs till, annars filtrerar Jekyll bort `_framework/` — mappar som börjar med understreck ignoreras som standard, och där ligger hela .NET-runtimen.
3. `index.html` kopieras till `404.html`, så att en direktlänk till `/elever/3` landar hos klientroutern i stället för en felsida.

Aktivera Pages under **Settings → Pages → Source: GitHub Actions**.

`.github/workflows/build.yml` bygger varje pull request utan att deploya.

---

## Arkitektur

```
Elevkollen.slnx
├── Elevkollen/          Blazor WebAssembly. All UI och all elevdata.
└── Elevkollen.Shared/   DTO:er och domänhjälpare. Inga beroenden.
```

**Konventioner**

- .NET 10, nullable och implicit usings på.
- MudBlazor till 100 %. Ingen egen CSS där en Mud-komponent räcker.
- Ingen duplicerad domänlogik. Utvecklingens text och betygssteg bor i `ProgressText`, färgen i `ProgressUi`.
- All elevdata går via `StudentStore`, som är enda stället som pratar med `js/db.js`.
- Nya IndexedDB-stores kräver höjd `DB_VERSION` och en **additiv** `onupgradeneeded` som aldrig rör befintlig data.
- Aggregering görs i ett svep med `ToLookup`/`Dictionary` — datamängden växer med varje termin.
- Svenska i UI och domänbegrepp, engelska i kod-identifierare.

Sortering använder `StringComparer.CurrentCulture` så att å, ä och ö hamnar rätt. Slå därför **inte** på `InvariantGlobalization`.

---

## Att göra före skarp drift

Inloggningen är en hårdkodad platshållare i `AuthState`, inte säkerhet. Den håller inte för publik hosting med riktiga elevuppgifter och måste bytas mot verklig autentisering först.

---

## Licens

MIT. Se [LICENSE](LICENSE).
