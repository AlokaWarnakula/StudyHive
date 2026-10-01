# StudyHive — Remaining Work (30 Sep → 6 Oct 2026)

This is the working plan for finishing StudyHive. Any agent or person starting a new session reads
this file first. The formal requirements are in
[`DOCS/StudyHive_Master_Project_Relay_Plan.html`](DOCS/StudyHive_Master_Project_Relay_Plan.html);
the screen → file → endpoint map is in
[`DOCS/S2_S3_S4_UI_Interface_Map.md`](DOCS/S2_S3_S4_UI_Interface_Map.md). If this plan and the master
plan disagree, the master plan wins on requirements; this plan wins on order and schedule.

**Submission: Mon 6 October 2026.** Build work finishes by Sat 4 October; Sun 5 October is buffer.

---

## 1. Where we are (30 Sep 2026, `main` @ `8bbf1d7`)

**All features are built, and the headline workflow works for real:** the student submits on
mobile → Planner, Scheduling, Resource and Validation agents run → a quotation is written → the
librarian approves on web (room booking + stock reservation in one transaction) → the student's
phone shows Approved with the quotation. This was verified on 30 Sep with the real agent and Grok,
nothing faked.

| Component | State |
|---|---|
| Foundation (auth, shells, CI) | ✅ Done |
| S1 Requests & Workflow + Planner | ✅ Done |
| S2 Rooms & Availability + Scheduling | ✅ Done |
| S3 Consumables & Stock + Resource | ✅ Done |
| S4 Costing, Validation, Approval & Audit (API, agent, web, mobile) | ✅ Done |
| Email (Brevo) | ❌ Table exists, nothing is queued or sent. **Decided: build it (Day 4).** |
| Dockerfiles (api, agent, web) + compose stack | ✅ Done (Day 4 Task 1, PR #20) |
| Deploy (Railway), APK | ❌ Not started. **No `mobile/android/` folder exists yet.** |
| README, ADRs, screenshots, k6, report, video | ❌ Not started |

Test baseline (must stay green): API **178**, agent **87**, web lint + **72** tests + build,
mobile analyze + **57** tests (`flutter test --concurrency=1`).

History of what was built and how is in git (PRs #7–#18) and in the previous version of this file
(`git show 8bbf1d7:PLAN.md`).

---

## 2. Rules for every task

1. **One task at a time.** Finish, test, commit. Never leave two things half-done.
2. **After every task run the full suite**, not just your new tests:
   ```
   cd api && dotnet test
   cd agent && .venv/Scripts/python -m pytest -q
   cd web && npm run lint && npm run test && npm run build
   cd mobile && flutter analyze && flutter test --concurrency=1
   ```
   Restore `mobile/windows/flutter/generated_*` after flutter runs. A red suite = stop and fix first.
3. **Git:** never commit to `main`. Each task gets a short branch cut from an up-to-date `main`
   (e.g. `s3/email-sender`). Commit (conventional messages: `feat(s3): ...`, `fix: ...`,
   `docs: ...`) only after review, push, open a PR into `main`, merge once CI is green. Do not
   delete branches.
4. **DevTeam:** Codex plans and reviews, Claude implements. Claude posts a room message whenever a
   Codex review is needed.
5. **House rules** already built into the API:
   - Lists: `[FromQuery] PageQuery` → `PagedResult<T>`; unknown `sortBy` = 400.
   - Errors: RFC 7807 ProblemDetails from the global handler. Never hand-roll error bodies.
   - Status codes: 201 create, 202 submit, 204 delete, 404 for not-yours, 409 conflict, 422 business-rule failure.
   - JSON camelCase + string enums; DB snake_case; money `numeric(12,2)`; instants `timestamptz`, ISO-8601 UTC over the wire, Asia/Colombo only for display.
6. **Tests:** API test classes run in parallel on one database, so never assume global counts stay
   fixed. Dev-DB tests must never lower seeded stock below `reserved_quantity`.
7. **Do not** create a second initial migration. Only add a migration if you change an entity.
8. **Never commit secrets.** Keys live in `.env` / `agent/.env` (gitignored). Never print `agent/.env`.

---

## 3. Remaining work

### Day 4 — Wed 1 Oct → Fri 3 Oct: Dockerfiles, email, APK, deploy, run-through

Do the tasks **in this order**, one branch and one PR each. Codex plans and reviews, Claude implements.

| # | Task | Branch | When | Done when |
|---|---|---|---|---|
| ✅ 0 | Commit this plan | `docs/plan-remaining` | Wed | Plan merged to `main` |
| ✅ 1 | Dockerfiles (api, agent, web) | `infra/dockerfiles` | Wed | All three run locally in Docker against the Docker DB |
| 2 | Email queue | `s3/email` | Wed–Thu | Approval queues one email; rolled-back approval queues none |
| 3 | Email sender | `s3/email` | Thu | Sent / retry / Failed / no-key tests green |
| ✔ | **Check before moving on** | — | Thu | Full suite green + one real email received through Brevo |
| 4 | Android folder + APK + QR stickers | `mobile/android` | Thu | APK installed on a phone, camera QR scan works on the real phone |
| 5 | Deploy to Railway | `infra/railway` (if code changes) | Fri | One full workflow on the live URLs |
| 6 | Full run-through + small leftovers | small fix branches | Fri | Happy path + every failure case run, each bug fixed with a test |

**Task 1 — Dockerfiles.** Branch `infra/dockerfiles`.
- [x] `api/Dockerfile`: multi-stage .NET 8 (sdk build → aspnet runtime), listens on `$PORT`
      (default 8080). The API does **not** apply migrations on start today (only the dev seeder
      runs): add a startup `Database.MigrateAsync()` (or an equivalent entrypoint step) so a fresh
      production DB gets the schema, without breaking the test suite.
- [x] `agent/Dockerfile`: Python 3.11 slim, `requirements.txt` only (not dev), uvicorn on
      `0.0.0.0:${PORT:-8001}`.
- [x] `web/Dockerfile`: `npm ci && npm run build` with `VITE_API_BASE_URL` as a build arg → small
      static server (e.g. nginx) with SPA fallback to `index.html`, listening on `$PORT`.
- [x] `.dockerignore` for each (`bin/`, `obj/`, `node_modules/`, `dist/`, `.venv/`, `.env`, tests).
- [x] Add `api`, `agent`, `web` services to `docker-compose.yml` (db stays as is) and verify:
      `/health`, `/swagger`, web login, and one workflow reaches the agent. Secrets come from env,
      never baked into images.
- [x] Full test suite still green; CI green.

**Task 2 + 3 — Email (Brevo)** — required by the master plan (S3 "email integration"). Branch `s3/email`.
- [ ] **Task 2, Queue:** write `email_notifications` rows (`Queued`) **inside the same transaction** as the
      event, so an email exists only if the event committed. The table has no body column: store
      `template` + `subject` + `booking_request_id`, and the sender renders the body at send time
      from the request, its latest quotation and decision:
  - Approval decision in `ApprovalsController` (Approved / Rejected / RevisionRequested) → student, with the librarian's comment and, for Approved, room, time and total.
  - Workflow ends `Failed` with `VALIDATION_FAILED` → student, with the revision note.
- [ ] **Task 3, Sender:** `Services/EmailSenderService.cs`, a `BackgroundService` polling due rows
      (`status = Queued AND (next_attempt_at IS NULL OR next_attempt_at <= now())`, index `ix_email_due`). Send via Brevo
      `POST https://api.brevo.com/v3/smtp/email` (header `api-key`). Success → `Sent` +
      `provider_message_id`. Failure → `attempt_count + 1`, `last_error`, back-off `next_attempt_at`;
      after `max_attempts` (3) → `Failed`. Behind an `IEmailProvider` interface so tests fake it.
- [ ] **Config:** `Brevo__ApiKey`, `Brevo__SenderEmail`, `Brevo__SenderName`. With no key (dev, CI)
      the sender stays off and rows stay `Queued`; the app never fails because email is missing.
- [ ] **Tests:** approval queues exactly one email and a rolled-back approval (409) queues none;
      sender marks Sent; retries then Failed after 3 attempts; no key → nothing sent.
- [ ] **Mobile copy:** "You will get a notification" (`create_request_screen.dart`,
      `approval_status_screen.dart`) → say "email".
- [ ] **Human (before the check):** create a free Brevo account, verify a sender address, create an API key.
- [ ] **Check:** full suite green, then approve one request locally with the real key and confirm the email arrives.

**Task 4 — Android APK.** Branch `mobile/android`.
- [ ] **`mobile/` has no `android/` folder** (only `web/` and `windows/`), so no APK can be built
      yet. Run `flutter create --platforms=android --org <org> .` in `mobile/`, set the app name,
      and allow cleartext HTTP only if the API URL is not HTTPS. The camera permission comes from
      `mobile_scanner`'s own manifest. Then build:
      `flutter build apk --dart-define=API_BASE_URL=<api URL>`; install it on a phone that isn't
      the developer's and **test the camera QR scan on the real phone** (it has only ever run in
      tests and the browser). Before the deploy, point it at the laptop's LAN IP.
- [ ] QR stickers: the web room page shows each room's QR code only as text. Print a real QR image
      for the demo room(s), either from a generator or by rendering it on the web room page.

**Task 5 — Deploy to Railway** (section 5).
- [ ] **Human:** create the Railway project and set the secrets.
- [ ] Production has no dev seeder: create the 4 role logins (one-off script), then run
      `infra/seed/demo-data.sql`.
- [ ] Smoke-test: `/health`, `/swagger`, web login, one full workflow on the live URLs.
- [ ] Rebuild the APK with the live API URL and reinstall it.

**Task 6a — Full run-through** (on the live deploy, or locally if deploy is blocked)
- [ ] Happy path: student (mobile) submits → watch workflow → librarian approves (web) → mobile
      Approved → email arrives → QR check-in.
- [ ] Failure cases: ineligible student (422), clashing slot, low-stock item, over budget, hostile
      objective, workflow failure (stop the agent → Failed with an error code).
- [ ] Fix every bug found, each with a test.

**Task 6b — Small leftovers**
- [ ] Audit rows for plain stock operations (stock-in, release, mark used).
- [ ] Mobile cost breakdown (`quotation_view_screen.dart`) shows a fixed `Proposed` label even when
      the quotation is Approved: use the real status.
- [ ] Mobile says check-in "Opens 15 min before", but `POST /api/room-bookings/{id}/check-in`
      has no time window: a student can check in any day. Enforce the window (422 outside it) with
      a test, or change the text.
- [ ] Mobile Home bell icon does nothing (`home_screen.dart`): link it to the booking list, or remove it.

### Day 5 — Sat 4 Oct: documentation & evidence
- [ ] README: live URLs, how to run locally, logins, architecture diagram, what's cut.
- [ ] 6 ADRs in `DOCS/adr/`: React state (Zustand), Flutter state (Provider), agent framework,
      **LLM = xAI Grok** (changed from Groq), workflow state storage + background queue,
      **hosting = Railway** (changed from Neon/Render/Vercel), email (Brevo + retry table).
- [ ] Swagger up to date; screenshots of every screen; CI green on `main`.
- [ ] Performance: k6, 50 concurrent users on `GET /api/rooms/available` → p50/p95/error rate.

### Sun 5 Oct: buffer + submission material
- [ ] Demo video (10 min)
- [ ] Final report
- [ ] AI usage log (date, tool, what it produced, what was changed, how verified)
- [ ] Individual reflection
- [ ] Viva rehearsal: each person explains their controller, migration, agent tool list and one test

### Mon 6 Oct: **submit**

### If we run out of time — cut in this order
1. k6 performance numbers
2. Reports polish (charts → simple tables)
3. Small leftovers (Task 6b)

**Never cut:** email (now a decided requirement), the working deploy and APK, the approval
transaction, the Validation agent, the concurrency/overlap/injection tests.

---

## 4. Local setup (Docker + demo data)

```bash
# Whole stack in Docker (dev logins, live agent): web http://localhost:8081, API http://localhost:8080/swagger
docker compose up -d --build
# Or run each piece yourself:
docker compose up -d db                                   # Postgres 16 on 127.0.0.1:5432
cd api && dotnet ef database update --project src/StudyHive.Api
cd api/src/StudyHive.Api && dotnet run --launch-profile http   # http://localhost:5299 (Swagger at /swagger), seeds logins
docker exec -i studyhive-db psql -U studyhive -d studyhive < infra/seed/demo-data.sql   # demo data
cd agent && .venv/Scripts/python -m uvicorn app.main:app --port 8001
cd web && npm run dev                                     # http://localhost:5173
cd mobile && flutter build web --dart-define=API_BASE_URL=http://localhost:5299   # then serve mobile/build/web on :8090
```

Local dev uses the Docker DB (`appsettings.Development.json`, DB user `studyhive`). The root `.env`
holds a remote connection string — **don't load it into your shell for local work**.

Logins (all share the dev password in [`ACCOUNTS.md`](ACCOUNTS.md)): `student@`, `librarian@`,
`storeofficer@`, `admin@studyhive.dev`; eligibility cases `nimal@` (clean), `kavya@` (1 penalty,
eligible), `suspended@` and `penalty@` (ineligible). A student may make 3 requests per week, so
use a fresh student for repeated end-to-end runs.

Demo data (`infra/seed/demo-data.sql`, safe to rerun): 6 rooms (one deactivated), a deliberate
clash on A-201 tomorrow 14:00–16:00, maintenance on B-210 the day after, 8 consumables with 3
low-stock, 3 suppliers, opening ledger rows.

**LLM:** xAI Grok via `agent/app/llm.py` `chat(instructions, data)`, the only network seam. The LLM
never decides anything: it writes the Planner summary and the Validation revision note, with a
deterministic fallback on any failure. Tests monkeypatch it; CI never calls Grok.

---

## 5. Hosting: everything on Railway

One Railway project, four services.

| Service | Source | Notes |
|---|---|---|
| **Postgres** | Railway Postgres plugin | Needs extensions `citext` and `btree_gist` (the migration creates them). |
| **api** | `api/` (Dockerfile, .NET 8) | Public domain. Applies migrations on deploy. `/health` and `/swagger` must load. |
| **agent** | `agent/` (Dockerfile, Python 3.11 + uvicorn) | **No public domain**: reached only over private networking (`http://agent.railway.internal:8001`). |
| **web** | `web/` (Dockerfile: `npm run build` → static server) | Public domain. `VITE_API_BASE_URL` = the API's public URL, set at **build** time. |

Environment variables
- **api:** `ConnectionStrings__Default` (convert Railway's `postgresql://USER:PASS@HOST:PORT/DB` to `Host=HOST;Port=PORT;Database=DB;Username=USER;Password=PASS;SSL Mode=Require;Trust Server Certificate=true`), `Jwt__SigningKey` (≥ 32 bytes, fresh), `Jwt__Issuer`, `Jwt__Audience`, `Agent__BaseUrl=http://agent.railway.internal:8001`, `Agent__InternalApiKey` (same as agent), `AllowedHosts`, `Cors__AllowedOrigins__0=<web URL>`, `ASPNETCORE_ENVIRONMENT=Production`, `Brevo__ApiKey`, `Brevo__SenderEmail`, `Brevo__SenderName`.
- **agent:** `INTERNAL_API_KEY` (real secret), `ENVIRONMENT=production`, `GROK_API_KEY`, `GROK_BASE_URL`, `GROK_MODEL`. Bind to `0.0.0.0` and Railway's `PORT`.
- **web:** `VITE_API_BASE_URL`.
- **mobile APK:** `flutter build apk --dart-define=API_BASE_URL=<api URL>`.

Before the demo: open every URL 5 minutes early; keep one completed workflow in the DB as a
fallback if Grok is rate-limited. Keep everything live until **21 October**.

---

## 6. Viva / evidence reminders
- Headline claims each have a test: **overlap rejected** (S2, exclusion constraint `no_double_booking` → 409), **last item can't be reserved twice** (S3, `chk_never_oversold`), **hostile objective changes nothing** (S4 golden case), **approval is one transaction** (S4, clash → 409 and nothing written).
- We store tool inputs/outputs, validation results, timings, errors. We do **not** store chain-of-thought, raw prompts or API keys (say this in the report).
- Keep the AI usage log and each person's reflection consistent with git history.
