# StudyHive — Remaining Work (4 Oct → 6 Oct 2026)

This is the working plan for finishing StudyHive. Any agent or person starting a new session reads
this file first. The formal requirements are in
[`DOCS/StudyHive_Master_Project_Relay_Plan.html`](DOCS/StudyHive_Master_Project_Relay_Plan.html);
the screen → file → endpoint map is in
[`DOCS/S2_S3_S4_UI_Interface_Map.md`](DOCS/S2_S3_S4_UI_Interface_Map.md). If this plan and the master
plan disagree, the master plan wins on requirements; this plan wins on order and schedule.

**Submission: Mon 6 October 2026.** The audit fix plan (3–4 Oct, PRs #27–#42) is finished; its
history is in git (`git show 9429904:PLAN.md`) and [`AUDIT.md`](AUDIT.md).

---

## 1. Where we are (4 Oct 2026, evening)

Everything is built, every audit blocker and must-fix is merged, and **the app is live on Railway**.

| Piece | State |
|---|---|
| API, agent, web, mobile (all audit fixes) | ✅ merged to `main` (PRs #27–#42), CI green |
| Railway: Postgres, api, agent (private), web | ✅ live — URLs in section 6 |
| Production logins + demo data | ✅ seeded (`production-bootstrap.sql`, then `demo-data.sql`) |
| `demo-data.sql` fix for a fresh database | 🟡 [PR #43](https://github.com/ItsAloka/StudyHive/pull/43) open, CI green — merge first |
| **Account self-service (passwords, profile)** | ❌ **missing — section 3** |
| Live run-through, docs, evidence | ❌ sections 4–5 |

Test baseline (must stay green, numbers only go up): API **241**, agent **87**, web lint + **109**
tests + build, mobile analyze + **84** tests (`flutter test --concurrency=1`).

### Why section 3 exists
A live check on 4 Oct found that **nobody can change a password** — not a student, not staff, not even
an Admin (the old decision D5 removed the controls and there is no endpoint). A forgotten password can
only be fixed by editing the database. A student also cannot correct their own name, department or
year after sign-up, and mobile registration never asks for the real student number (the API invents
`REG-xxxxxxxx…`). Section 3 fixes this. **Decided:** new accounts stay **active immediately — no
admin approval of accounts** (every booking is still approved by a Librarian). Profile pictures are
**out of scope**.

---

## 2. Rules for every task

1. **One task at a time per agent.** Finish, test, commit. Never leave two things half-done.
2. **After every task run the full suite**, not just your new tests:
   ```
   cd api && dotnet test            # stop studyhive-api first if it runs with the Brevo key
   cd agent && .venv/Scripts/python -m pytest -q
   cd web && npm run lint && npm run test && npm run build
   cd mobile && flutter analyze && flutter test --concurrency=1
   ```
   Restore `mobile/windows/flutter/generated_*` after flutter runs. A red suite = stop and fix first.
3. **Git:** never commit to `main`. Each task gets its own branch cut from an up-to-date `main`
   (names below). Conventional commits (`feat(api): …`, `feat(mobile): …`, `feat(web): …`).
   Push, open a PR into `main`, merge once CI is green and the review is approved. Do not delete
   branches. **Merging to `main` redeploys Railway automatically** (api/agent/web) — say so in the
   DevTeam room before merging.
4. **Order:** API first (it defines the contracts), then mobile and web consume them. Shared contract
   changes are announced in the DevTeam room before the clients are built.
5. **House rules** already built into the API:
   - Lists: `[FromQuery] PageQuery` → `PagedResult<T>`; unknown `sortBy` = 400.
   - Errors: RFC 7807 ProblemDetails from the global handler. Never hand-roll error bodies.
   - Status codes: 201 create, 202 submit, 204 no content, 404 for not-yours, 409 conflict, 422 business-rule failure.
   - JSON camelCase + string enums; DB snake_case; money `numeric(12,2)`; instants `timestamptz`, ISO-8601 UTC over the wire, **Asia/Colombo only for display**.
   - Passwords: BCrypt work factor 11 via `IPasswordHasher`; rules match registration (8–100 chars).
   - Auth endpoints use the `RateLimitPolicies.AuthEndpoints` limiter; staff actions write an audit row via `IAuditWriter`.
6. **Tests:** API test classes run in parallel on one database, so never assume global counts stay
   fixed. Every new endpoint gets success, validation, auth (401/403) and not-yours tests.
7. **Migrations:** none are expected for section 3 (all columns already exist). Never a second
   initial migration.
8. **Never commit secrets or the production password.** Keys live in `.env` / `agent/.env`
   (gitignored) and in Railway. Never print `agent/.env`.

---

## 3. Account self-service — Sun 5 Oct (do first)

### 3.0 Before starting (human, ~10 min)
- [ ] Merge [PR #43](https://github.com/ItsAloka/StudyHive/pull/43) (demo-data fix).
- [ ] Railway → Postgres → Settings → Networking: **Public Access is OFF** (it was turned on only to
      run the seed scripts).

### 3.1 API — branch `feat/api-account-self-service`

**a) Change own password** — any signed-in user (Student, Librarian, StoreOfficer, Admin).
- `POST /api/auth/change-password` (`?client=web` supported like login/refresh), body
  `{ currentPassword, newPassword }`, rate limited with `AuthEndpoints`.
- Wrong `currentPassword` → **400** ValidationProblem with a field error on `currentPassword`.
  `newPassword` 8–100 chars and different from the current one, else 400.
- On success: hash and save, **revoke every refresh token of that user** (sign out other devices),
  then issue a fresh token pair exactly like login (web: new refresh cookie) so the current device
  stays signed in. Write an audit row (`user.password_changed`, no password data in it).
- Note for the report: other devices' access tokens stay valid until they expire
  (`Jwt:AccessTokenMinutes`); their refresh fails, so they are signed out at the next refresh.

**b) Student edits own profile.**
- `PUT /api/student-profiles/me` — `StudentOnly`, body
  `{ fullName, studentNumber, department, yearOfStudy }` (lengths as in registration /
  `CreateStudentProfileRequest`; year 1–5).
- `studentNumber` must be unique → **409** `Student number already registered` if another profile has it.
- Returns `StudentProfileResponse`. `fullName` is stored on `users.full_name`.
- **Not editable here:** email, `maxBookingsPerWeek`, `penaltyPoints`, `suspendedUntil`, `isActive`
  (Admin-only via the existing `PUT /api/student-profiles/{id}`). 404 if the student has no profile yet
  (they use the existing onboarding `POST /api/student-profiles`).

**c) Admin resets a password** (the "forgot password" path: the student asks the library desk).
- `PUT /api/users/{id}/password` — `AdminOnly`, body `{ newPassword }` (8–100). Implement only this
  action in `Controllers/Admin/UsersController.cs`; the other scaffold actions stay 501.
- Revoke every refresh token of that user; audit row `user.password_reset` with the admin's id.
- 404 for an unknown user. An Admin cannot reset their own password here (use 3.1a) → 422.

**d) Real student number at sign-up.**
- `RegisterRequest` gets optional `studentNumber` (same rules as onboarding). When it is supplied with
  `department` + `yearOfStudy`, use it (unique → 409) instead of `REG-…`. Old clients without it keep
  working.

- [ ] Tests for a–d (including: changed password logs in, old one fails; other session's refresh is
      rejected after a change/reset; student cannot edit limits/penalties through `/me`; Student and
      Librarian get 403 on the reset endpoint; duplicate student number → 409 on `/me` and register).
- [ ] Swagger shows the three new endpoints.

### 3.2 Mobile — branch `feat/mobile-account-self-service` (after 3.1 merges)
- [ ] **Profile → "Edit profile"** screen: full name, student number, department, year; Save calls
      `PUT /api/student-profiles/me`; field errors and the 409 shown on the right field; profile
      refreshes after saving. Email, limit, penalties and suspension stay read-only.
- [ ] **Profile → "Change password"** screen: current, new, confirm new (must match, 8+ chars);
      calls `POST /api/auth/change-password`; stores the returned tokens; success message; a wrong
      current password shows on that field.
- [ ] Replace the note "To change your password, ask at the library desk." with
      "Forgot your password? Ask at the library desk — an admin can reset it." on the **login** screen
      only.
- [ ] **Registration** asks for the student number and sends `studentNumber`.
- [ ] Widget tests for both new screens and the new register field.
- [ ] Rebuild the release APK with the live URL (section 6) and reinstall on the emulator / phone.

### 3.3 Web — branch `feat/web-account-self-service` (after 3.1 merges; parallel with 3.2)
- [ ] **Change password** for every staff role: an item next to "Sign out" in the header opens a
      dialog (current, new, confirm) → `POST /api/auth/change-password?client=web`.
- [ ] **Students page (Admin only): "Reset password"** action per student → dialog with a new
      temporary password + confirm → `PUT /api/users/{userId}/password`; success toast says to give
      the password to the student in person. Hidden for Librarian/StoreOfficer
      (add `users.resetPassword` to `web/src/auth/permissions.ts`).
- [ ] Tests for both dialogs and the role gate.

### 3.4 Check (after 3.1–3.3 merge and Railway redeploys)
- [ ] On the **live** site: student changes password on the phone → old password fails, new works;
      student edits profile → the librarian sees the new name/department on the Students page;
      admin resets a student's password → the student signs in with it.
- [ ] Update `README.md` (what's built / known limits) and record decision **D5 (revised)** in
      section 7: change password + admin reset + student profile edit are built; self-service
      "forgot password" by email is future work.

---

## 4. Live run-through (Task 6) — Sun 5 Oct

Use the live URLs (section 6). Logins: `student@`, `librarian@`, `storeofficer@`, `admin@studyhive.dev`
plus `nimal@` (clean), `kavya@` (1 penalty, eligible), `suspended@` and `penalty@` (ineligible) — all
share the **production password** chosen on 4 Oct (not the dev one; never written in the repo).

- [ ] Happy path: student (mobile) submits → workflow → librarian approves (web) → mobile Approved
      → email arrives → QR check-in **inside the window** → booking completes → usage report shows it.
- [ ] Reject path and ask-for-change → edit and resend path.
- [ ] Failure cases: ineligible student (422), clashing slot (A-201 tomorrow 14:00–16:00 is booked by
      the demo data), low-stock item, over budget, hostile objective, workflow failure (stop the agent
      → Failed with an error code), check-in too early (422), cancel then approve (409).
- [ ] Fix every bug found, each with a test.
- [ ] Tick Task 5 evidence: `/health` + `/swagger` OK, dev password fails on the live site (checked
      4 Oct ✅), production logins work (✅ librarian on web).

---

## 5. Documentation & evidence — Sun 5 Oct

- [ ] README: live URLs, how to run locally, logins (no passwords), architecture diagram, what's cut.
- [ ] ADRs in `DOCS/adr/`: React state (Zustand), Flutter state (Provider), agent framework,
      **LLM = xAI Grok**, workflow state storage + background queue, **hosting = Railway**,
      email (Brevo + retry table), **D1 web session (refresh cookie)**.
- [ ] Swagger up to date; screenshots of every screen; CI green on `main`.
- [ ] Performance: k6, 50 concurrent users on `GET /api/rooms/available` → p50/p95/error rate.
- [ ] Demo video (10 min), final report, AI usage log, individual reflection, viva rehearsal (human).

### Mon 6 Oct: **submit**

### If we run out of time — cut in this order
1. Web staff "Change password" dialog (3.3 first item) — the admin reset stays.
2. Registration student-number field (3.1d / 3.2) — the admin can't fix it either, so mention it.
3. k6 run (describe the plan instead).
Never cut: the student change-password screen, the admin reset, the live run-through.

---

## 6. Hosting (live on Railway)

Project **sparkling-forgiveness**, environment `production`, all services deploy from GitHub `main`.

| Service | URL / address | Notes |
|---|---|---|
| **web** | https://web-production-a74ec.up.railway.app | Root `/web`. `VITE_API_BASE_URL` (build time), `PORT=8080`. |
| **api** | https://api-production-1198b.up.railway.app (`/health`, `/swagger`) | Root `/api`. Applies migrations on start; Production never runs the dev seeder. |
| **agent** | `http://agent.railway.internal:8001` (private only, no public domain) | Root `/agent`. `PORT=8001`, `HOST=::`. |
| **Postgres** | `postgres.railway.internal` (private) | Public Access only while seeding, then off. |

Variables (names only — values live in Railway):
- **api:** `ASPNETCORE_ENVIRONMENT=Production`, `ConnectionStrings__Default` built from
  `${{Postgres.PGHOST}}` / `PGPORT` / `PGDATABASE` / `PGUSER` / `PGPASSWORD` with
  `SSL Mode=Prefer;Trust Server Certificate=true`, `Jwt__SigningKey` (fresh), `Jwt__Issuer=studyhive`,
  `Jwt__Audience=studyhive-clients`, `Agent__BaseUrl`, `Agent__InternalApiKey` (fresh),
  `AllowedHosts=api-production-1198b.up.railway.app` (literal; the API refuses `*` outside
  Development, and `${{RAILWAY_PUBLIC_DOMAIN}}` was empty before the domain existed), `PORT=8080`,
  `Cors__AllowedOrigins__0=<web URL>`, `CheckIn__OpensMinutesBefore=15`, `Brevo__ApiKey`,
  `Brevo__SenderEmail`, `Brevo__SenderName`.
- **agent:** `ENVIRONMENT=production`, `INTERNAL_API_KEY=${{API.Agent__InternalApiKey}}`,
  `GROK_API_KEY`, `PORT=8001`, `HOST=::`.
- **web:** `VITE_API_BASE_URL=https://api-production-1198b.up.railway.app`, `PORT=8080`.
- **mobile APK:** `flutter build apk --release --dart-define=API_BASE_URL=https://api-production-1198b.up.railway.app`.

Seeding a fresh database (no local `psql` needed; Postgres Public Access on, then off again):
```bash
docker run --rm -i postgres:16-alpine psql "$DATABASE_PUBLIC_URL" -v ON_ERROR_STOP=1 -v staff_password='…' < infra/seed/production-bootstrap.sql
docker run --rm -i postgres:16-alpine psql "$DATABASE_PUBLIC_URL" -v ON_ERROR_STOP=1 < infra/seed/demo-data.sql
```
Railway tips: after changing variables click **Deploy** (Restart keeps the old values). The deploy
safety checklist is in [`README.md`](README.md) ("Deploying to Railway: safety checklist").

Before the demo: open every URL 5 minutes early (the emulator's network can need a cold boot); keep
one completed workflow in the DB as a fallback if Grok is rate-limited. Keep everything live until
**21 October** (the free trial is 30 days / $5 — watch the usage).

### Local setup (Docker + demo data)
```bash
docker compose up -d --build     # web http://localhost:8081, API http://localhost:8080/swagger, dev logins
docker exec -i studyhive-db psql -U studyhive -d studyhive < infra/seed/demo-data.sql
```
Local logins share the dev password in [`ACCOUNTS.md`](ACCOUNTS.md). Emulator APK for the local
stack: `--dart-define=API_BASE_URL=http://10.0.2.2:8080`.

---

## 7. Decisions and viva / evidence reminders
- **D1** web session uses an httpOnly refresh cookie (`SameSite=None; Secure` in production).
- **D5 (revised 4 Oct):** change password (all roles), admin password reset and student profile edit
  are built in section 3; emailed "forgot password" links are future work. **No admin approval of
  new accounts** — accounts are active at once, bookings are approved by a Librarian.
- **D2** full user management (roles UI) is future work.
- Headline claims each have a test: **overlap rejected** (S2, exclusion constraint `no_double_booking` → 409), **last item can't be reserved twice** (S3, `chk_never_oversold`), **hostile objective changes nothing** (S4 golden case), **approval is one transaction** (S4, clash → 409 and nothing written), **cancelled requests cannot be approved** (A1) and **check-in only inside its window** (A3).
- We store tool inputs/outputs, validation results, timings, errors. We do **not** store chain-of-thought, raw prompts, passwords or API keys (say this in the report).
- The audit (`AUDIT.md`) and its fix-status table are evidence of testing and honest defect handling — mention it in the report.
- Keep the AI usage log and each person's reflection consistent with git history.
