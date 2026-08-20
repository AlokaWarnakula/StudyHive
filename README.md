# StudyHive

University study-room & library-resource booking system. Flutter student app → ASP.NET Core API →
PostgreSQL → internal LangGraph agent workflow → React staff dashboard approval → back to Flutter.

Full spec: [`DOCS/StudyHive_Master_Project_Relay_Plan.html`](DOCS/StudyHive_Master_Project_Relay_Plan.html).
Supplied mockups: [`UI/`](UI/) (web + mobile, offline HTML).

## Layout

```
api/        ASP.NET Core 8 Web API + EF Core (PostgreSQL)
web/        React + Vite + TypeScript — Librarian / Store Officer / Admin dashboard
mobile/     Flutter — Student app
agent/      FastAPI + LangGraph — internal only, reachable only from api/
infra/      (reserved for deployment/infra assets beyond docker-compose.yml)
.github/    CI workflows
DOCS/       Project plan and specification (reference, do not edit casually)
UI/         Supplied UI mockups (reference, do not edit)
```

Clients never call `agent/` directly. Everything goes through `api/`, which is the only component
allowed to reach PostgreSQL, the agent service, and Brevo (email).

## Tech stack & locked decisions (ADRs — see DOCS sec. 13)

| Layer | Choice |
|---|---|
| API | ASP.NET Core 8, EF Core + Npgsql, JWT bearer auth |
| Web state | Zustand |
| Mobile state | Provider |
| Agent framework | LangGraph (FastAPI host) |
| LLM | Groq `llama-3.3-70b` (Gemini Flash failover) |
| Database | PostgreSQL (local: Docker; hosted: Neon) |
| Background jobs | `IHostedService` + `Channel<T>` — no Redis, no Hangfire |

## Shared conventions (binding — see DOCS sec. 12)

- Postgres columns: `snake_case`. C# entities: `PascalCase`. JSON over the wire: `camelCase`. Wired
  once in `api/src/StudyHive.Api/Program.cs` — don't fight it with manual overrides.
- Every list endpoint: `?page=1&pageSize=20&sortBy=...&sortDir=desc&search=...` →
  `{ items, page, pageSize, totalItems, totalPages }`. Max `pageSize` is 100. Unknown `sortBy` is a 400.
- Every error is an RFC 7807 `ProblemDetails` body, produced by the one global exception handler in
  `api/src/StudyHive.Api/Middleware/ExceptionHandlingMiddleware.cs`. Never hand-roll an error body.
- All stored instants are `timestamptz` / `DateTimeOffset`, never bare `DateTime`. Over the wire:
  ISO-8601 UTC with `Z`. Convert to `Asia/Colombo` only at display time in React/Flutter.
- Branches: `s{your-student-number}/feature/...` or `.../fix/...`. One PR per issue, reviewed by
  someone else, never merged by its author. `main` is protected.

## Local setup

### 0. Prerequisites

- .NET 8 SDK, Node.js (LTS recommended), Python 3.11+, Docker Desktop
- Flutter SDK for `mobile/` (see [`mobile/README.md`](mobile/README.md)) — Android SDK is only
  needed later, for an Android emulator/APK; Chrome works for day-to-day development

### 1. Database

```bash
docker compose up -d db
```

Starts Postgres on `localhost:5432` (db `studyhive`, user/password `studyhive`/`studyhive_dev` — dev
only, matches `api/src/StudyHive.Api/appsettings.Development.json`). Full connection details, reset
and backup commands: [`DOCS/database.md`](DOCS/database.md).

### 2. API

```bash
cd api
dotnet ef database update --project src/StudyHive.Api --startup-project src/StudyHive.Api
dotnet run --project src/StudyHive.Api
```

Swagger at `http://localhost:5299/swagger` (or whatever port `dotnet run` prints), health at `/health`.
Run tests: `dotnet test` from `api/`.

### 3. Web dashboard

```bash
cd web
cp .env.example .env
npm install
npm run dev
```

Opens at `http://localhost:5173`. Run tests: `npm run test`.

### 4. Agent service

```bash
cd agent
cp .env.example .env
python -m venv .venv
./.venv/Scripts/pip install -r requirements-dev.txt   # Windows; use .venv/bin/pip on macOS/Linux
./.venv/Scripts/python -m uvicorn app.main:app --reload --port 8001
```

`ENVIRONMENT` defaults to `production` if unset (fail-safe) — `.env.example` sets it to `development`
so the placeholder `INTERNAL_API_KEY` works locally. Outside Development, a missing or still-placeholder
key refuses to start; see `agent/app/settings.py`.

Health at `http://localhost:8001/health`. Run tests: `pytest` from `agent/` (with the venv active).

### 5. Mobile app

```bash
cd mobile
flutter pub get
flutter run -d chrome --dart-define=API_BASE_URL=http://localhost:5299
```

Verified: `flutter analyze` and `flutter test` run clean, `flutter build web` succeeds. See
[`mobile/README.md`](mobile/README.md) for platform notes (Android SDK is optional, not required
for Chrome development).

## Test accounts (local Development only)

Seeded automatically at API startup when `ASPNETCORE_ENVIRONMENT=Development`, from
`api/src/StudyHive.Api/appsettings.Development.json`'s `DevSeed:Users` section — never present
outside Development, and never real credentials. All four share one password:

| Email | Role |
|---|---|
| `student@studyhive.dev` | Student |
| `librarian@studyhive.dev` | Librarian |
| `storeofficer@studyhive.dev` | StoreOfficer |
| `admin@studyhive.dev` | Admin |

Password: `Dev-Only-Passw0rd!`. The React dashboard only accepts staff roles; the Flutter app only
accepts Student — each client rejects the other's accounts with a clear message rather than a
confusing failed-login state. Anyone can also self-register a new Student account from the mobile
app or `POST /api/auth/register` — staff accounts can't be created that way on purpose.

## Relay build order

Shared foundation (this scaffold) → **S1** Requests & Workflow + Planner agent → **S2** Rooms &
Availability + Scheduling agent → **S3** Consumables & Stock + Resource agent → **S4** Costing,
Validation, Approval & Audit. Nobody merges into another owner's component — see DOCS sec. 03/04 for
the full ownership table and handoff gates.
