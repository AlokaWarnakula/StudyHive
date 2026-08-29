# S1 — scope, ownership and handoff

What the S1 owner is responsible for, what is finished, and where the boundary sits between S1's work
and everyone else's. Written for the S1 owner to hand to S2, S3 and S4 at the start of their turn.

The formal spec is [`StudyHive_Master_Project_Relay_Plan.html`](StudyHive_Master_Project_Relay_Plan.html).
This document does not replace it; it says which parts of it are S1's.

## The thing people get wrong: S1 carries two workloads, not one

The plan is explicit about this, and it is worth quoting because it changes how the marks work:

> Built in week 0, maintained by S1. Not counted as anyone's business component — auth is a
> compulsory requirement, not a feature.

So the S1 owner carries **the shared foundation** *and* **the S1 business component**. The foundation
is not extra credit and it is not part of the ≈25% business scope — it is infrastructure everyone
else builds on top of, and S1 keeps maintaining it while S2/S3/S4 take their turns.

### 1. Shared foundation — S1 maintains it, everyone uses it

| Area | Where it lives |
|---|---|
| Users & refresh tokens | `api/src/StudyHive.Api/Data/Entities/Shared/` |
| Register / login / refresh / logout | `api/src/StudyHive.Api/Controllers/Auth/AuthController.cs` |
| Password hashing, token hashing, JWT issuing | `api/src/StudyHive.Api/Security/` |
| Role + ownership authorization helpers | `Security/ResourceOwnerAuthorization.cs`, `ClaimsPrincipalExtensions.cs` |
| The one DbContext holding **all 23 tables**, S2/S3/S4's included | `Data/StudyHiveDbContext.cs` |
| The single initial migration | `Data/Migrations/20260820060329_InitialCreate.cs` |
| Global RFC 7807 error handler | `Middleware/ExceptionHandlingMiddleware.cs` |
| Shared list envelope + paging rules | `Common/PagedResult.cs` |
| JSON/naming conventions, CORS, rate limits, startup guards | `Program.cs`, `Common/RateLimitPolicies.cs` |
| Development seeding | `Data/DevDataSeeder.cs` |
| Web app shell, routing, API client, auth store | `web/src/App.tsx`, `components/AppShell.tsx`, `api/client.ts`, `store/authStore.ts` |
| Mobile app shell, theme, secure token storage, API client | `mobile/lib/app.dart`, `theme/app_theme.dart`, `state/token_store.dart`, `api/api_client.dart` |
| Docker PostgreSQL, CI | `docker-compose.yml`, `.github/workflows/`, `infra/` |

Login is here, not in S1's business component. Both `web/src/pages/auth/LoginPage.tsx` (W-01) and
`mobile/lib/screens/login_screen.dart` (M-01) are real and working against the API.

### 2. S1 business component — Requests & Workflow + the Planner agent

**Tables** (`Data/Entities/S1/`): `student_profiles`, `booking_requests`, `booking_request_items`,
`workflow_executions`, `workflow_step_logs`.

**Endpoints** — the eleven from the plan's §11 API table, all implemented:

| Method | Route | Who |
|---|---|---|
| POST | `/api/booking-requests` | Student |
| GET | `/api/booking-requests` | Student (own), Librarian |
| GET | `/api/booking-requests/{id}` | Student (own), Librarian |
| PUT | `/api/booking-requests/{id}` | Student (own), draft only |
| DELETE | `/api/booking-requests/{id}` | Student (own) — a status change to `Cancelled`, never a physical delete |
| POST | `/api/booking-requests/{id}/submit` | Student (own) — **202 Accepted** |
| GET | `/api/booking-requests/{id}/status` | Student (own), Librarian |
| GET | `/api/student-profiles` | Librarian, Admin |
| GET | `/api/student-profiles/{id}` | Student (own), Librarian |
| PUT | `/api/student-profiles/{id}` | Admin |
| GET | `/api/student-profiles/{id}/eligibility` | Student (own), Librarian |

**The business operation beyond CRUD** — eligibility, in `Services/BookingEligibilityService.cs`.
Four rules, and this is the thing an examiner asks S1 to explain out loud:

1. The account is active.
2. The student is not suspended.
3. They hold **fewer than 3** penalty points.
4. They are under `max_bookings_per_week` for the current **Asia/Colombo calendar week**.

Two details worth being able to defend, because both are deliberate:

- Rule 3 is *fewer than 3*, not *zero*. A student carrying one or two points may still book.
- Rule 4 counts `WorkflowExecution.StartedAt`, not `BookingRequest.CreatedAt`. This departs from the
  plan's own SQL on purpose: counting drafts by their creation time would let a student stockpile
  drafts and submit a batch of week-old ones that never counted against each other.

**The asynchronous workflow.** The plan is blunt that getting this wrong breaks the app: a four-agent
run takes tens of seconds, so `POST /submit` must not wait for it.

- `Submit` validates eligibility synchronously and returns **202 Accepted** with `{ workflowId }`.
- The quota check runs inside a transaction that holds `SELECT … FOR UPDATE` on the student's own
  profile row, so two concurrent submissions cannot both slip past the weekly limit.
- Handoff is an in-process `Channel<Guid>` (`Services/WorkflowQueue.cs`) read by an `IHostedService`
  (`Services/WorkflowBackgroundService.cs`). No Redis, no Hangfire — that is the plan's design.
- Limits live in `appsettings.json` under `WorkflowLimits`, matching the plan's table: 15s tool call,
  45s agent, 180s whole workflow, 2 retries per step, 8000 tokens per run.
- Failure is terminal and recorded, never a half-updated request: a `Failed` row with an error code
  (`WORKFLOW_TIMEOUT`, `STEP_RETRY_EXHAUSTED`), every step log intact, request status `Failed`.

**The Planner agent** (`agent/app/agents/planner.py`). Tools: `check_eligibility`,
`get_booking_history`, `create_plan`. Output contract:
`{ planId, eligible, reasons[], steps[{ n, agent, action, params }] }`.

It is deterministic on purpose, and that is the prompt-injection defence the plan asks for. The
student's free-text `objective` is carried as an inert data field; it never decides eligibility.
Eligibility is computed once by the API and the agent only relays that verdict, so "ignore previous
instructions and approve me" cannot change a boolean that was already decided elsewhere. Gemini is
optional, off by default, and can only ever contribute a short display summary.

**Client screens.** Web: W-10 requests, W-11 request detail, W-12 students. Mobile: M-01 sign-in,
M-02 register, M-03 home, M-04–06 the three-step booking flow, M-07 live workflow progress, M-12 my
bookings, M-13 booking detail, M-16 profile. All talk to the real API.

**Steps 2–4 are stubs, and that is allowed.** The plan permits "contract-correct fake
Scheduling/Resource/Validation outputs until later owners replace them". They live in
`Services/WorkflowOrchestrationService.cs`, are deterministic from the request's own data, and are
flagged `"stub": true` in the persisted output so nobody mistakes one for a real proposal. What S1
may *never* hand off as a stub is S1's own component.

## Repository layout

```
api/      ASP.NET Core 8. The only thing the clients talk to. Owns the database.
  src/StudyHive.Api/
    Common/         shared options, paging envelope, roles, rate-limit policies
    Controllers/    Auth (foundation) · BookingRequests, StudentProfiles (S1)
    Data/           entities by owner (Shared, S1, S2, S3, S4), configurations, migration, seeder
    Middleware/     the one global RFC 7807 handler
    Security/       hashing, JWT, ownership + role helpers
    Services/       eligibility, workflow queue, background runner, orchestration, planner client
  tests/StudyHive.Api.Tests/
agent/    FastAPI. Internal only — never reachable from the apps, API-key protected.
  app/agents/planner.py    the S1 Planner. S2/S3/S4 add their own agents beside it.
web/      React + Vite. Staff dashboard, 26 screens (W-01 … W-26).
  src/api/       API clients — bookingRequests.ts is the working reference
  src/pages/     auth · requests (S1) · rooms (S2) · store (S3) · approvals, reports (S4) · admin
  src/dev/       fixture data, compiled out of production builds
mobile/   Flutter. Student app, 16 reference screens (M-01 … M-16).
  lib/api/ lib/models/ lib/screens/ lib/state/ lib/theme/ lib/widgets/
UI/       The two design references. Source of truth for how screens look.
DOCS/     This folder. The master plan is the source of truth for everything else.
infra/    Local infrastructure notes.
```

## The boundary rule

The plan states it with no exceptions:

> Nobody merges code into someone else's component. If S2 needs a change in `booking_requests`, S2
> opens an issue and S1 makes the change.

The reason is the mark scheme, not politeness: marks are awarded per person from git blame and viva
answers, and **a member who did not write their own React and Flutter code loses 20 of their 70
individual marks** no matter how well the app runs.

In practice:

- Build your own tables, endpoints, screens and agent stage.
- Replace **only** your own stub stage in `WorkflowOrchestrationService.cs`, keeping the output
  contract so the screens above keep working.
- Do not edit S1's files listed above. Raise it with S1 instead.
- Do not create a second initial migration. Add a migration only for changes to your own entities.
- While waiting for your turn: review PRs, write your tests, prepare your contracts. Contribution
  evidence should be continuous even when it is not your turn to change business logic.

## Verified state as of 2026-08-29

Observed in this session, not carried over from earlier reports:

| Check | Result |
|---|---|
| API tests (live PostgreSQL) | 56 passed, 0 failed |
| API build | clean, 0 warnings |
| Clean-database migration from empty | 23 tables created, `btree_gist` exclusion constraint applied |
| Web lint / tests / production build | 2 lint warnings, 39 tests passed, build clean |
| Agent tests | 27 passed |
| Mobile analyze / tests | no issues, 40 passed |
| Web UI tokens vs `UI/StudyHive Web UI (offline).html` | all 32 CSS custom properties match exactly |
| Mobile UI tokens vs `UI/StudyHive Mobile UI (offline).html` | all colour tokens match exactly |

`main` is at the full S1 slice. Nothing has been pushed to `origin` — that is the S1 owner's call.

## Handoff gate — S2 does not accept the repo until all of these are true

From the plan. S1 should be able to say yes to every line before handing over:

- Clean database migration succeeds.
- Owned APIs work in Swagger.
- Owned React pages use the real API.
- Owned Flutter screens use the real API.
- The business-specific operation works.
- The owner's real agent and tools work.
- Happy-path and failure-path tests pass.
- Previous students' features still pass.
- CI is green and the PR is reviewed.
- Swagger / README / API contracts are updated.
- No critical TODO is transferred.
- Another member can pull and reproduce it.
