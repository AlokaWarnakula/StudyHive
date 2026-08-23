# StudyHive — foundation & shared-UI owner guide

Written for whoever owns the **shared foundation and UI shells** — the person who set up the scaffold
everyone else builds on, and who wants a clear picture of the whole system before handing pieces of it
off. The formal spec is [`StudyHive_Master_Project_Relay_Plan.html`](StudyHive_Master_Project_Relay_Plan.html)
("the master plan") — this document explains how the pieces that already exist fit together, points at
exactly where they live, and lists only commands that have actually been run against this repo.

Companion doc: [`Project_Guide.md`](Project_Guide.md) is the shorter, plainer-language version of "what
is this project." Read that first if you want the 2-minute version; this one is the reference.

## 1. Architecture, in one picture

```
Flutter (student)  ──┐
                      ├──►  ASP.NET Core API  ──►  PostgreSQL
React (staff)      ──┘            │
                                   ▼
                     FastAPI agent service (internal only)
```

- **Neither client ever talks to the agent service directly.** The API is the only component allowed
  to reach PostgreSQL, the agent service, or any external service (email). This is enforced two ways:
  the agent service only accepts requests carrying a shared `X-Internal-Api-Key` header
  (`agent/app/security.py`), and neither `web/` nor `mobile/` code ever references the agent's port.
- **One shared `StudyHiveDbContext`** (`api/src/StudyHive.Api/Data/StudyHiveDbContext.cs`) holds every
  table for all four business components — there's no per-feature database.
- **One JSON contract**, shared by both clients: Postgres columns `snake_case`, C# entities
  `PascalCase`, JSON over the wire `camelCase`. Configured once in `Program.cs`
  (`JsonNamingPolicy.CamelCase` + `JsonStringEnumConverter`), never overridden per-endpoint.

## 2. The relay model this repo follows

Business functionality is built **sequentially, one owner at a time**, each handing off a fully working
system to the next (master plan §04). The shared foundation goes first; it is scaffold only — no
business logic of its own.

| Phase | Owns | Status as of this doc |
|---|---|---|
| Shared foundation | Auth/RBAC, schema skeleton + migration, client shells, agent security boundary, CI | ✅ Complete and verified (see §5) |
| S1 — Requests & Workflow + Planner agent | Booking request lifecycle, eligibility, workflow orchestration | ✅ Complete and verified (see §5) |
| S2 — Rooms & Availability + Scheduling agent | Rooms, equipment, maintenance windows, conflict-free search | ⏳ Not started |
| S3 — Consumables & Stock + Resource agent | Consumables, suppliers, stock ledger, no-oversell reservation | ⏳ Not started |
| S4 — Costing, Validation, Approval & Audit | Quotations, deterministic validation, approval transaction, audit log | ⏳ Not started |

The rule with no exceptions (master plan §03): **nobody merges code into another owner's component.**
If S2 needs a change inside `booking_requests`, S2 opens an issue and S1 (or whoever currently owns
that table) makes the change.

## 3. What the shared foundation actually provides today

This is the part you built. Concretely, in the repo:

- **Auth & RBAC** — `api/src/StudyHive.Api/Controllers/Auth/AuthController.cs`: register (always
  creates a Student — staff accounts are never self-service, and current clients atomically create
  the StudentProfile too), login, refresh-token rotation, logout,
  `/me`, an Admin-only user directory, and the "own record, or staff" ownership pattern
  (`Security/ResourceOwnerAuthorization.cs`) that every later feature reuses instead of hand-rolling
  ownership checks. Passwords: BCrypt. Refresh tokens: stored only as a SHA-256 hash, never the raw
  value. Roles: `Student`, `Librarian`, `StoreOfficer`, `Admin` (`Common/Roles.cs`).
- **Locked schema & one initial migration** — all 23 tables across Shared/S1/S2/S3/S4 exist as EF Core
  entities (`Data/Entities/`) and configurations (`Data/Configurations/`), baked into a single
  `InitialCreate` migration. Every documented CHECK constraint, unique/partial index, and delete
  behavior from the master plan §10 is already in that migration — later owners add their own business
  logic on top, they don't touch the schema-authoring pattern.
- **Agent security boundary** — `agent/app/security.py`: every route except `/health` requires
  `X-Internal-Api-Key`, checked with a constant-time comparison (`hmac.compare_digest`) so the check
  itself can't leak the key through response-time differences. `agent/app/settings.py` defaults
  `environment` to `"production"` (fail-closed) — the placeholder key only works when a developer
  explicitly opts in via `agent/.env`'s `ENVIRONMENT=development`.
- **Fail-fast configuration everywhere** — outside Development, the API refuses to start with a missing
  JWT signing key, missing DB connection string, wildcard `AllowedHosts`, or (as of S1) a missing
  agent-service key/URL. Same pattern each time: no known-value fallback outside local dev.
- **React shell** — `web/src/App.tsx` (routing), `components/AppShell.tsx` (side nav + sign-out),
  `routes/ProtectedRoute.tsx` (redirects unauthenticated/wrong-role users to `/login`),
  `store/authStore.ts` (Zustand — access/refresh tokens are memory-only by design, never
  `localStorage`, so a page reload means signing in again).
- **Flutter shell** — `mobile/lib/app.dart` (switches Login/Home on auth state),
  `screens/home_screen.dart` (bottom-nav shell), `state/auth_provider.dart` (session state; tokens
  persisted via `flutter_secure_storage`, never plain prefs), `state/token_store.dart` (abstracted so
  tests can swap in an in-memory fake instead of hitting a real platform channel).
- **Design tokens taken from the supplied mockups, not invented** — `web/src/index.css`'s `:root`
  palette (`--color-accent: #5980a6` etc.) and `mobile/lib/theme/app_theme.dart`'s `AppColors` use the
  same hex values, and the Flutter file says so directly in its own comment: "Colors lifted from
  `UI/StudyHive Mobile UI (offline).html`". If you add a screen and need a color, reuse a token from
  one of those two files rather than picking a new hex value.
- **CI** — `.github/workflows/ci.yml` runs the API, web, agent and mobile checks on every push.

## 4. The supplied UI mockups: what they are, how they've been used

`UI/StudyHive Mobile UI (offline).html` and `UI/StudyHive Web UI (offline).html` are large, single-file
offline exports (500–700 KB each) — effectively a built preview bundle, not hand-editable source. They
are the **visual source of truth** for layout and color, but they're impractical to reverse-engineer
line-by-line. The practical approach taken so far: pull out concrete, checkable facts (the color
palette, page names, key labels) and build clean, real, functional screens that honor those facts,
rather than attempting pixel-for-pixel reproduction of a minified export. Treat them the same way for
S2–S4: confirm your screen's colors/labels/layout intent against the mockup, but don't block progress
on matching it byte-for-byte.

## 5. Verified state (commands actually run against this repo)

```bash
docker compose up -d db                                            # Postgres, healthy
dotnet ef database update --project api/src/StudyHive.Api \
  --startup-project api/src/StudyHive.Api                          # no pending migrations
cd api && dotnet test                                               # runs api/StudyHive.sln
cd web && npm run lint && npm run test && npm run build
cd agent && ./.venv/Scripts/python.exe -m pytest -q                # Windows; .venv/bin/... elsewhere
cd mobile && flutter pub get && flutter analyze && flutter test && flutter build web
```

Latest backend results (this session): API Release build 0 warnings/0 errors; `dotnet test` 54/54 passed (Auth,
Schema, RateLimiting, StudentProfiles, BookingRequests — all against the real Postgres, not an
in-memory fake, plus deterministic seed coverage); agent `pytest` 27/27; mobile
`flutter analyze` 0 issues, `flutter test` 19/19, `flutter build web --release` succeeds. A real booking request was
also submitted through the live API + live agent service end to end (not just automated tests) and
confirmed reaching `PendingApproval` with its full 4-step plan, then verified rendering correctly in
the React dashboard in an actual browser session.

### Local URLs & dev accounts

| Service | URL |
|---|---|
| API (Swagger + health) | `http://localhost:5299/swagger`, `/health` |
| Agent service (health only, unauthenticated) | `http://localhost:8001/health` |
| Web dashboard | `http://localhost:5173` |
| Mobile (Chrome target) | launched via `flutter run -d chrome` |

Seeded automatically at API startup, Development environment only (`api/src/StudyHive.Api/appsettings.Development.json`
→ `DevSeed:Users`), password `Dev-Only-Passw0rd!` for all four:

| Email | Role | Use it in |
|---|---|---|
| `student@studyhive.dev` | Student | Flutter app |
| `librarian@studyhive.dev` | Librarian | React dashboard |
| `storeofficer@studyhive.dev` | StoreOfficer | React dashboard |
| `admin@studyhive.dev` | Admin | React dashboard |

Each client rejects the other's account type with a clear message rather than a confusing failed
login (see `web/src/pages/auth/LoginPage.tsx` and `mobile/lib/state/auth_provider.dart`).

Development startup also seeds three fixed-id preview consumables plus eight fixed-id S1 requests
and six workflow executions with step logs. The data spans every status needed by the supplied UI,
is safe under concurrent startup, and is never reached outside `IsDevelopment()`.

## 6. Ownership boundaries — which paths belong to which owner

This is the practical, file-level version of master plan §03's ownership table. "Shared" paths are
edited only through a reviewed PR that affects everyone; a component's own paths are edited only by
that component's current owner.

| Area | Shared / foundation | S1 (done) | S2 | S3 | S4 |
|---|---|---|---|---|---|
| DB entities | `Data/Entities/Shared/*`, `Data/Configurations/SharedConfigurations.cs` | `Data/Entities/S1/*`, `Data/Configurations/S1Configurations.cs` | `Data/Entities/S2/*`, `...S2Configurations.cs` | `Data/Entities/S3/*`, `...S3Configurations.cs` | `Data/Entities/S4/*`, `...S4Configurations.cs` |
| API controllers | `Controllers/Auth/*` | `Controllers/BookingRequests/*`, `Controllers/StudentProfiles/*` | rooms/availability controllers (new) | consumables/stock controllers (new) | quotations/approvals/audit controllers (new) |
| API services | `Security/*`, `Middleware/*`, `Common/*` | `Services/BookingEligibilityService.cs`, `PlannerClient.cs`, `WorkflowQueue.cs`, `WorkflowBackgroundService.cs`, `WorkflowOrchestrationService.cs` | scheduling/availability services (new) | stock/reservation services (new) | quotation/approval services (new) |
| Agent | `agent/app/security.py`, `settings.py`, `main.py` health route | `agent/app/agents/planner.py`, `schemas.py` | `agent/app/agents/scheduling.py` (new) | `agent/app/agents/resource.py` (new) | `agent/app/agents/validation.py` (new) |
| Web pages | `App.tsx`, `components/AppShell.tsx`, `routes/`, `pages/auth/*` | `pages/librarian/Requests*.tsx`, `pages/librarian/StudentProfile*.tsx`, `api/bookingRequests.ts`, `api/studentProfiles.ts` | `pages/librarian/RoomsPage.tsx` (placeholder → real) | `pages/store/StockPage.tsx` (placeholder → real) | `pages/admin/AdminPage.tsx` (placeholder → real) |
| Mobile screens | `app.dart`, `state/auth_provider.dart`, `state/token_store.dart`, `screens/login_screen.dart`, `screens/home_screen.dart` | `screens/create_request_screen.dart`, `track_screen.dart`, `booking_detail_screen.dart`, `profile_screen.dart`, `state/booking_requests_provider.dart`, `state/profile_provider.dart` | room/QR check-in screens (new) | — | — |

`agent/app/agents/__init__.py` already documents each future agent's allow-listed tool names — read
it before starting S2/S3/S4's agent module.

## 7. Handing off the baseline (and later, each relay stage)

Branching, already in use: `s{n}/feature-name` per owner, one PR per component, `main` protected,
never merged by its own author (master plan §04, `Foundation_Completion_and_S1_Readiness_Plan.md`
§"Git and branch process"). Current branch for this work: `s1/requests-workflow-planner`.

**The next owner does not accept the repo until every one of these is true** (master plan §04
handoff gate, verbatim):

- Clean database migration succeeds
- Owned APIs work in Swagger
- Owned React pages use the real API
- Owned Flutter screens use the real API
- Business-specific operation works
- Owner's real agent + tools work
- Happy-path and failure-path tests pass
- Previous students' features still pass
- CI is green and PR reviewed
- Swagger/README/API contracts updated
- No critical TODO is transferred
- Another member can pull and reproduce it

S1 meets all twelve as of this doc (see §5 for the evidence and the root `README.md`'s "S1 —
Requests & Workflow + Planner: implemented" section for the exact endpoint/screen/test list).

## 8. Glossary

- **BookingRequest status** — `Draft → Submitted → Processing → PendingApproval → Approved/Rejected →
  Completed/Cancelled/Failed`. `Draft` and terminal statuses (`Rejected`/`Completed`/`Cancelled`/
  `Failed`) don't count against a student's weekly booking quota; everything else does.
- **WorkflowExecution status** — `Started → InProgress → PendingApproval → Approved/Rejected/Failed →
  Completed`. One `BookingRequest` can have at most one *active* `WorkflowExecution` at a time
  (enforced by a filtered unique DB index), but keeps a full history of past attempts.
- **WorkflowStepLog** — one row per agent step (Planner, then Scheduling/Resource/Validation), storing
  the tool name, input/output JSON, a Pass/Fail/Warning validation result, timing, and any error — but
  never chain-of-thought, raw prompts, or API keys (master plan §11 "What we do not store").
  Deliberately durable even on failure: a failed workflow still leaves every prior step log intact.
- **"stub": true** — a marker S1 puts on the Scheduling/Resource/Validation step output while those
  agents don't exist yet, so nobody mistakes a placeholder proposal for a real one. S2/S3/S4 replace
  the stub-producing code, not the step-logging mechanism around it.
- **Planner Agent** — the one agent S1 owns. Deterministic (no LLM call in the current implementation),
  never touches the database, and only ever trusts the eligibility verdict the API already computed —
  it cannot be talked into approving something via the free-text `objective` field. See
  `agent/app/agents/planner.py`.
- **Internal API key** — the shared secret (`X-Internal-Api-Key`) that lets the ASP.NET Core API call
  the FastAPI agent service. Configured as `Agent:InternalApiKey` on the API side and
  `INTERNAL_API_KEY` on the agent side; must match. Never present outside each side's own
  Development-only config.
- **ResourceOwner policy** — the shared authorization rule ("own record, or any staff role") almost
  every ownership check in this codebase uses instead of a bespoke check per feature.
- **PagedResult** — the one list-response shape every list endpoint returns:
  `{ items, page, pageSize, totalItems, totalPages }`, from `?page=&pageSize=&sortBy=&sortDir=&search=`.
- **ProblemDetails** — the one error-response shape (RFC 7807), produced by
  `Middleware/ExceptionHandlingMiddleware.cs`; no controller hand-rolls its own error body.
