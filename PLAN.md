# StudyHive — Remaining Work (3 Oct → 6 Oct 2026)

This is the working plan for finishing StudyHive. Any agent or person starting a new session reads
this file first. The formal requirements are in
[`DOCS/StudyHive_Master_Project_Relay_Plan.html`](DOCS/StudyHive_Master_Project_Relay_Plan.html);
the screen → file → endpoint map is in
[`DOCS/S2_S3_S4_UI_Interface_Map.md`](DOCS/S2_S3_S4_UI_Interface_Map.md). If this plan and the master
plan disagree, the master plan wins on requirements; this plan wins on order and schedule.

**Submission: Mon 6 October 2026.** **Sat 4 Oct: fix everything in [`AUDIT.md`](AUDIT.md).**
Sun 5 Oct: deploy, run-through, documentation. Mon 6 Oct: submit.

---

## 1. Where we are (3 Oct 2026)

All features are built and the headline workflow works end to end (student submits on mobile → four
agents → quotation → librarian approves on web → room booked + stock reserved → email → student sees
Approved). Docker stack, email (Brevo) and the Android scaffold are done.

On 3 Oct a full functional audit ([`AUDIT.md`](AUDIT.md)) walked every mobile screen, every web page
for all three staff roles, and the end-to-end approve / reject / ask-for-change / cancel / check-in
paths, submitting real actions on test data. It found **5 blockers**, about **25 must-fix** issues and
about **20 nice-to-have** issues. **All of them are in scope for Sat 4 Oct** — section 3 below is the fix
plan.

| Component | State |
|---|---|
| Foundation, S1, S2, S3, S4 (API, agent, web, mobile) | ✅ Built — **audit fixes pending** (section 3) |
| Email (Brevo) | ✅ Done (PR #23). The compose stack currently runs with empty Brevo vars (emails only queue). |
| Dockerfiles + compose | ✅ Done (PR #20) |
| Android folder + debug APK | ✅ Scaffold done (PR #25, **open — merge before any fix branch**) |
| Deploy (Railway) | ❌ After the fixes (Task 5) |
| README, ADRs, screenshots, k6, report, video | ❌ Sun 5 Oct |

Test baseline (must stay green): API **188**, agent **87**, web lint + **72** tests + build,
mobile analyze + **58** tests (`flutter test --concurrency=1`). Every fix below adds tests, so these
numbers only go up.

---

## 2. Rules for every task

1. **One fix group at a time per agent.** Finish, test, commit. Never leave two things half-done.
2. **After every fix group run the full suite**, not just your new tests:
   ```
   cd api && dotnet test            # stop studyhive-api first if it runs with the Brevo key
   cd agent && .venv/Scripts/python -m pytest -q
   cd web && npm run lint && npm run test && npm run build
   cd mobile && flutter analyze && flutter test --concurrency=1
   ```
   Restore `mobile/windows/flutter/generated_*` after flutter runs. A red suite = stop and fix first.
3. **Git:** never commit to `main`. Each fix group gets its own branch cut from an up-to-date `main`
   (names in section 3). Conventional commits (`fix(api): …`, `fix(mobile): …`, `fix(web): …`).
   Push, open a PR into `main`, merge once CI is green and the other agent approved. Do not delete
   branches.
4. **Who does what (Sat 4 Oct):** **Claude** (one session) owns **API + mobile** fixes. **Codex** owns
   **web** fixes. Each reviews the other's PRs. Shared contract changes (new response fields, roles,
   endpoints) are made by Claude in the API **first**, announced in the DevTeam room, then consumed
   by Codex in the web. Never edit the other agent's area without asking in the room.
5. **House rules** already built into the API:
   - Lists: `[FromQuery] PageQuery` → `PagedResult<T>`; unknown `sortBy` = 400.
   - Errors: RFC 7807 ProblemDetails from the global handler. Never hand-roll error bodies.
   - Status codes: 201 create, 202 submit, 204 delete, 404 for not-yours, 409 conflict, 422 business-rule failure.
   - JSON camelCase + string enums; DB snake_case; money `numeric(12,2)`; instants `timestamptz`, ISO-8601 UTC over the wire, **Asia/Colombo only for display**.
6. **Tests:** API test classes run in parallel on one database, so never assume global counts stay
   fixed. Dev-DB tests must never lower seeded stock below `reserved_quantity`.
7. **Migrations:** only add one if you change an entity. Never a second initial migration.
8. **Never commit secrets.** Keys live in `.env` / `agent/.env` (gitignored). Never print `agent/.env`.
9. **Every fix references its audit ID** (C-xx, CW-xx, W-xx) in the commit message and PR body.

---

## 3. Audit fix plan — Sat 4 Oct

Order is **highest risk first**: data corruption and broken core flows (Phase A), then everything a
user or examiner will trip over (Phase B), then polish (Phase C). Inside a phase, Claude and Codex
work in parallel on their own areas. IDs refer to [`AUDIT.md`](AUDIT.md).

### 3.0 Before starting (both, ~15 min)

- [x] Merge PR #25 (`mobile/android`), then the docs PR with `AUDIT.md` + this `PLAN.md` (PR #26).
- [x] Pull `main`; full suite green on a clean checkout (API 188, agent 87, web 72, mobile 58).
- [ ] Human decisions (defaults below apply if nobody objects):
  - **D1 Web session across reload (W-01):** keep the access token in memory; move the **refresh
    token into an httpOnly, Secure, SameSite cookie** set by the API (`/api/auth/login` and
    `/api/auth/refresh` with `?client=web`), and call `/api/auth/refresh` on app start. Never
    localStorage. *Fallback if the cookie work overruns: refresh-on-401 only, and document that a
    reload signs staff out.* → **Done with the cookie (PR #31 API, PR #32 web); no fallback needed.**
  - **D2 Admin Users / Settings (W-25/26):** **hide both nav items and routes in production** for
    this release; record "user management" as future work. Not built.
  - **D3 Room photos (C-21):** **remove the photo placeholders** from mobile and web; no image upload.
  - **D4 In-app notifications (C-13):** **email is the notification channel.** The bell opens
    My bookings; the Profile "Notifications" row is removed.
  - **D5 Forgot / change password (C-13):** **remove both controls** for this release ("ask the
    library" note on login). No reset flow.

### Phase A — blockers (start first, merge before Phase B)

#### A1. Booking lifecycle integrity — Claude, branch `fix/api-booking-lifecycle` — ✅ [PR #27](https://github.com/ItsAloka/StudyHive/pull/27)
Covers **C-01, CW-07 (stuck Pending reservations), C-07, C-06 (API part), C-15 (no-shows)**.

- **Approve must check the request (C-01).** In `ApprovalsController.Create` reject the decision
  with **409 `request-not-pending`** unless `bookingRequest.Status == PendingApproval`.
- **Cancel must clean up (C-01, CW-07).** In `BookingRequestsController.Cancel`, inside one
  transaction: set the latest `Proposed` quotation to **`Superseded`** (already allowed by
  `ck_quotations_status`), release every `Pending` reservation (move
  `ReleasePendingReservationsAsync` into a shared service), and set the latest workflow execution to
  `Rejected` with `error_code = CANCELLED_BY_STUDENT` (there is no `Cancelled` workflow status — no
  migration needed). The approval queue then no longer lists it.
- **Cancel an approved booking (C-07).** Add `Approved` to the cancellable statuses **only before
  the first room booking starts**. On cancel: room bookings → `Cancelled`, `Reserved` reservations →
  `Released` **with a `Release` stock transaction** (so stock and the usage report agree), queue a
  `BookingCancelled` email. After start → 409 "This booking has already started".
- **Room-booking lifecycle (C-06, C-15).** New `BookingLifecycleService` (hosted, every 5 min, plus
  a method tests can call): a `Confirmed` room booking whose `ends_at` has passed becomes
  `Completed` if `checked_in_at` is set, else `NoShow`. When every room booking of an Approved
  request is Completed/NoShow, the request becomes `Completed`. (Penalty points for no-shows: **not**
  in this release.)
- **Expose the booking on the request (C-06, C-14, CW-12).** Add to `BookingRequestResponse`:
  `roomBookings: [{ id, roomId, roomName, startsAt, endsAt, status, checkedInAt }]`. Announce in the
  room — Codex uses it in B-web.
- **Tests:** approve after student cancel → 409 and nothing booked; cancel PendingApproval with items
  → reservation Released, quotation not Proposed, not in `GET /api/approvals?status=Pending`;
  cancel Approved before start → room booking Cancelled, stock back, Release transaction, email row;
  cancel after start → 409; lifecycle: checked-in past booking → Completed, request Completed;
  not checked-in → NoShow; response contains `roomBookings`.
- **Verify:** rerun the audit's C-01 script (cancel → approve) and get 409.

#### A2. Weekly limit and ask-for-change — Claude, branch `fix/api-eligibility-revision` — ✅ [PR #28](https://github.com/ItsAloka/StudyHive/pull/28)
Covers **C-03, C-02 (API part)**.

- **Off-by-one (C-03).** The workflow's eligibility check must not count its own execution. Give
  `BookingEligibilityService.EvaluateAsync` an `excludeBookingRequestId` parameter and **count
  distinct booking requests** submitted this Colombo week (not executions), excluding the one being
  evaluated. `Submit` and the orchestrator then agree, and a resubmitted request is not counted twice.
- **Resubmit after "ask for a change" (C-02).** Allow `PUT /api/booking-requests/{id}` when status is
  `Draft` **or `RevisionRequested`** (editing a RevisionRequested request sets it back to `Draft`), and
  allow `POST …/submit` from `RevisionRequested` directly. The old quotation becomes `Superseded`;
  the new workflow writes quotation version 2.
- **Tests:** a student with 2 submissions this week submits a 3rd → workflow reaches PendingApproval;
  4th → 422 at submit; RevisionRequested → PUT 200 → submit 202 → new quotation version 2; the
  resubmission does not consume another weekly slot.
- **Verify:** the audit's C-03 and C-02 scripts now pass.

#### A3. Check-in integrity — Claude, branch `fix/api-checkin` — ✅ [PR #29](https://github.com/ItsAloka/StudyHive/pull/29)
Covers **C-04, C-10, C-12 (check-in part)**.

- **Time window (C-04).** In `RoomBookingsController.CheckIn` return **422 `outside-check-in-window`**
  unless `startsAt − 15 min ≤ now ≤ endsAt`; detail says when it opens. Make the window a setting
  (`CheckIn:OpensMinutesBefore`, default 15) so the demo can be run.
- **Hide QR codes from students (C-10).** `RoomResponse.qrCode` is returned only to staff roles
  (null for Student). Mobile room detail stops showing it.
- **Wrong code (C-12).** When the id is a booking request id and no booking matches the code, return
  the documented **422 "Invalid room QR code"** instead of a bare 404.
- **Tests:** early → 422, inside window → 200, after end → 422; student `GET /api/rooms/{id}` has no
  `qrCode`, librarian has it; wrong code via request id → 422 with detail.

#### A4. Mobile session — Claude, branch `fix/mobile-session` — ✅ [PR #30](https://github.com/ItsAloka/StudyHive/pull/30)
Covers **C-05, C-12 (mobile client part)**.

- **Refresh on 401 (C-05).** `ApiClient` gets an `onUnauthorized` hook; `AuthProvider` implements it
  by calling `/api/auth/refresh` with the stored refresh token **once** (single in-flight refresh
  shared by concurrent calls), saving the new pair, and retrying the original request. If refresh
  fails → clear session → Login screen with "Your session expired, please sign in again".
- **Field errors (C-12).** `ApiException` keeps `errors`; `toString()` returns the first field
  message when `detail` is missing. Add a request **timeout** (15 s) with a friendly message.
- **Tests:** fake client returns 401 then 200 after refresh → screen shows data; refresh 401 →
  logged out; concurrent 401s trigger one refresh; validation body → field message shown.
- **Verify on emulator:** leave the app 31 minutes, open a booking → it loads. (Done with the API's
  `Jwt__AccessTokenMinutes=2`: after expiry the booking detail loaded and the refresh token rotated.)

#### A5. Web session — Claude (Codex absent), branch `fix/web-session` — ✅ [PR #32](https://github.com/ItsAloka/StudyHive/pull/32)
Covers **W-01, web token refresh, CW-02**.

- Implement **D1** (API cookie part is small: Claude adds the `client=web` cookie mode to
  `AuthController` login/refresh/logout in A2's branch or a tiny `fix/api-auth-cookie` branch first;
  CORS `AllowCredentials` for the configured origins only).
  ✅ API part: [PR #31](https://github.com/ItsAloka/StudyHive/pull/31). `?client=web` sets the
  `studyhive_refresh` cookie (httpOnly, Secure, Path=/api/auth, SameSite Lax locally / None in
  production) and leaves the token out of the body; refresh/logout send `{}` with credentials.
- `apiFetch`: on 401 → one shared refresh → retry once; refresh failure → sign-out to `/login` with
  "Session expired". On app start → try refresh, so a reload keeps the session. Keep the deep link
  (`/login?next=/rooms`) and return there after sign-in (part of CW-09).
- **Field errors (CW-02):** `ApiError.message` uses the first `errors` entry when `detail` is missing.
- Request **timeout** via `AbortController` (15 s).
- **Tests:** 401 → refresh → retry; refresh fails → login; reload restores session (mock refresh);
  validation errors show the field message.

#### A6. Deploy safety — Claude (doc) + human, before Task 5 — ✅ [PR #33](https://github.com/ItsAloka/StudyHive/pull/33)
Covers **committed development keys**.

- [x] Railway API service: `ASPNETCORE_ENVIRONMENT=Production`, fresh `Jwt__SigningKey` and
      `Agent__InternalApiKey` (never the values in `appsettings.Development.json`), and the
      Development settings file is never mounted or copied into the image (Dockerignore already
      excludes it — verify with `docker run … ls`).
- [x] Add this checklist to section 5 and to the README deploy steps. (Image check 4 Oct: `/app`
      holds only `appsettings.json`, `ASPNETCORE_ENVIRONMENT=Production`. The human still ticks
      the Railway items at deploy time.)

### Phase B — must-fix (after Phase A merges)

#### B1. API role alignment, audit log, maintenance, reports — Claude, branch `fix/api-roles-audit` — ✅ [PR #34](https://github.com/ItsAloka/StudyHive/pull/34)
Covers **CW-04, CW-05 (API part), CW-06 (API part), CW-07 (report), C-15 (hours), CW-08 (API part)**.

- **One role table (CW-05).** Decide per area and apply to both API and web (Codex mirrors it):
  Admin may **read** maintenance windows, suppliers, stock reservations and stock transactions;
  Librarian may read reservations. Admin **writes** stay where they are except: maintenance create
  stays Librarian-only (web hides the form for Admin). Consumable `PUT` honours `isActive`
  (StoreOfficer can deactivate/reactivate).
- **Audit log (CW-04).** Add `IAuditWriter.Write(action, entityType, entityId, details)` and call it
  in the same `SaveChanges` for: room create/update/delete, room equipment add/remove, equipment
  create/update, maintenance create/cancel, consumable create/update/deactivate, stock-in,
  reservation issue/release, supplier create/update, student profile update, booking cancel.
- **Maintenance (CW-06).** `maintenance_windows` has no status column, so add
  `DELETE /api/maintenance-windows/{id}` (Librarian; hard delete, **future windows only**, 409 for
  started ones; the audit row keeps the record) and `PUT` for future windows. `POST` that overlaps Confirmed bookings returns **409
  `maintenance-overlaps-bookings`** listing them, unless `force=true`; with `force`, queue a
  `MaintenanceConflict` email to each affected student.
- **Reports.** Consumable usage "released" counts released **reservations** in range, not only
  stock transactions (CW-07). Room usage `bookingsByHour` in **Asia/Colombo** hours; add a
  `checkedIn` count beside `noShows` (C-15, CW-12).
- **Students list (CW-08).** `StudentProfileResponse` adds `fullName`, `email`, `isSuspended`
  (`suspendedUntil > now`); search also matches name/email. Admin `PUT` accepts `penaltyPoints`
  and `suspendedUntil`.
- **Tests:** each new role rule (allowed + forbidden); one audit row per write type; maintenance
  overlap 409 / force 201 + emails; report released count; hour bucket for a 10:00 Colombo booking
  = 10; student list returns name/email and suspended flag.

#### B2. Mobile must-fix — Claude, branch `fix/mobile-ux` — ✅ [PR #35](https://github.com/ItsAloka/StudyHive/pull/35)
Covers **C-06 (UI), C-07 (UI), C-08, C-09, C-11, C-13, C-14, C-16, C-02 (UI)**.

- **Booking state from `roomBookings` (C-06, C-14).** Detail and list show the **assigned room and
  real slot** (Colombo time, "Mon 6 Oct · 14:00–16:00 · Quiet Study 101"), a "Checked in at 14:05"
  state, and hide Check-in and Cancel after check-in. Timeline gets "Approved by librarian" and
  "Checked in" steps. Completed/NoShow go to **Past**.
- **Cancel (C-07).** Confirmation dialog; shown only while the API allows it (A1 rules).
- **Revision (C-02).** RevisionRequested detail shows the librarian's comment and an **"Edit and
  resend"** button that opens the request form pre-filled → PUT → submit.
- **Fresh data (C-08).** Refresh Home/Bookings on tab switch, on return from any pushed screen, and
  on app resume (`WidgetsBindingObserver`).
- **Keep the choice (C-09).** `CreateRequestScreen({initialDate, initialFrom, initialTo, roomName})`
  — Book this room / Use slot pass them; the form shows "Preferred room: X (the librarian confirms
  the final room)". Past slots today are disabled. (Room preference is advisory; no API field.)
- **Orphan drafts (C-11).** On submit failure keep the created draft id and retry **submit** on the
  same draft; Draft tiles get **Send** and **Delete** actions.
- **Inert controls (C-13, D4, D5).** Bell → My bookings; remove Rooms search icon, Forgot password,
  Change password, Help and Notifications rows (or Help → a static screen with library contact).
- **Allowance (C-16).** "Bookings this week" uses `GET /api/student-profiles/{id}/eligibility`
  (add `usedThisWeek` to that response in B1) instead of counting locally.
- **Tests:** widget tests for checked-in detail (no buttons), Past tab for Completed, revision edit
  flow, draft Send, prefilled form from a slot, tab-switch refresh.

#### B3. Web must-fix — Claude (Codex absent), branch `fix/web-ux` — ✅ [PR #36](https://github.com/ItsAloka/StudyHive/pull/36)
Covers **CW-01, CW-03, CW-05 (web), CW-06 (web), CW-07 (web), CW-08 (web), CW-12, W-13, W-25/26 (D2)**.

- **Dialog errors (CW-01).** Rooms/Room detail/Equipment dialogs render their error inside the
  dialog (pass `error` into `<Dialog>` like the store pages do).
- **Sign out everywhere (CW-03).** Remove `showUser={false}` from all 18 pages (or make the shell
  always show the user block).
- **One role table (CW-05).** A single `web/src/auth/permissions.ts` (`can(role, action)`) drives
  routes, nav links and buttons, matching B1. Add **Deactivate** on consumable detail and room
  edit already has Active. Hide Admin's maintenance form.
- **Maintenance (CW-06).** Cancel/Edit buttons on future windows; on 409 overlap show the affected
  bookings and a "Schedule anyway and email the students" confirm.
- **Reservations (CW-07).** Columns: student, request objective (link), room, booking date; filter
  "Due today". Issue/Release ask for confirmation.
- **Students (CW-08).** Name and email columns; status shows Suspended (until date); admin panel
  edits penalty points and suspended-until.
- **Check-ins (CW-12).** Request detail shows the room booking(s) with check-in time; room calendar
  shows booker objective and Checked in / No-show; room-usage report shows checked-in count.
- **Printable QR (W-13).** Render the room's QR on room detail (`qrcode` package from npm) with a
  **Print** button (print CSS: QR + room name only). Use it to print the demo stickers (Task 4).
- **Admin Users/Settings (D2).** Hide nav items and routes in production builds.
- **Tests:** dialog shows server error; shell shows Sign out on a former `showUser={false}` page;
  permissions table unit test per role; QR renders for a room; reservations columns render.

### Phase C — nice-to-have (same day, after Phase B; cut from the bottom if late)

**Claude — branch `fix/mobile-polish` (+ small API bits):** ✅ [PR #38](https://github.com/ItsAloka/StudyHive/pull/38) (C-23 = release APK in 3.9)
- C-17 "Free now" only when `/api/rooms/available` says the room is free for the next hour; otherwise no tag.
- C-18 Greeting by time of day (morning / afternoon / evening).
- C-19 Room detail header inside `SafeArea`.
- C-20 Approval status ↔ Cost breakdown use `pushReplacement` / pop instead of stacking.
- C-21 / D3 Remove photo/avatar placeholders; real launcher icon + splash via `flutter_launcher_icons`
  and `flutter_native_splash` (dev dependencies) with the StudyHive logo; login logo = real asset.
- C-22 / Codex hygiene: delete stale SCAFFOLD/TODO comments in `mobile/lib/api/quotations_api.dart`,
  `state/quotations_provider.dart`.
- C-23 Build the demo APK as **release** (or `--dart-define=ENABLE_DEMO_DATA=false`).
- CW-11 (mobile half) all times formatted in Colombo time, no raw ISO strings.

**Codex — branch `fix/web-polish`:** ✅ [PR #39](https://github.com/ItsAloka/StudyHive/pull/39) (done by Claude; CW-14 supplier links cut)
- CW-09 Forbidden or unknown route while signed in → "You don't have access" / "Page not found"
  inside the shell with a link to Dashboard; keep `?next=` after sign-in.
- CW-10 Request detail: item names instead of GUIDs (use the consumables list); delete the stale
  "S3 consumables API, which is not built yet" text; timeline from real status history (workflow
  steps + decision + room booking); unknown id → "Request not found" with a back link.
- CW-11 Format all room lines and slot times (`Quiet Study 101 · Mon 13 Oct, 10:00–11:00`); no
  seconds in Requests table.
- CW-13 Confirmation for Reject, Ask for a change, Issue, Release, Remove equipment (Approve keeps a
  single click but shows a toast with Undo-free summary).
- CW-14 Supplier on Stock in (optional select) and "Suppliers" tile on consumable detail from
  `consumable_suppliers` (Claude adds `GET /api/consumables/{id}` → `suppliers[]` in B1 if time
  allows; otherwise drop this item).
- Login page "library photograph" placeholder → real image or remove (D3); stale comments in
  `web/src/api/users.ts`.

### 3.9 End of Sat 4 Oct — check (both)

- [x] Every audit ID is either fixed (PR link) or explicitly cut (reason) in a table appended to
      `AUDIT.md` ("Fix status"). Checked by script: all 42 IDs in the findings have a row.
- [x] Full suite green on the top of the stack (`fix/mobile-quote-line` = main after #38–#40):
      API 241, agent 87, web 109, mobile 84 (baseline 188 / 87 / 72 / 58); CI green on #38, #39, #40.
- [x] Re-run the audit scripts (4 Oct, compose stack, fresh students):
      C-01 cancel 204 → approve 409 `already-decided`, 0 room bookings ·
      C-02 revise → PUT 200 → submit 202 → PendingApproval, quotation v2 ·
      C-03 requests 1–3 PendingApproval, 4th submit 422 ·
      C-04 approved booking for 14 Oct, check-in today → 422 `outside-check-in-window` ·
      C-05 release APK, 2-minute tokens (`Jwt__AccessTokenMinutes=2`, same path as 31 min):
      restored 10:51:52Z, booking detail opened 10:55:27Z → refresh token rotated, detail loaded ·
      CW-01 duplicate QR → "A room with this QR code already exists." inside the dialog ·
      CW-03 Sign out on Reports, Rooms, Equipment, Maintenance, Students, Workflows, Calendar, Room usage.
- [x] Rebuild the APK (release, not debuggable) and reinstall on the emulator (Pixel_8_Pro).

### If we run out of time — cut in this order
1. Phase C items, from the bottom of each list up.
2. CW-14 supplier links, CW-12 calendar details (keep request-detail check-in), C-16 allowance.
3. D1 cookie persistence → fall back to refresh-on-401 only (document reload sign-out).

**Never cut:** all of Phase A, CW-01, CW-03, CW-04, CW-05, C-06, C-08, C-13, W-13 (printable QR).

---

## 4. After the fixes

### Task 5 — Deploy to Railway (Sun 5 Oct morning)
- [ ] **Human:** create the Railway project and set the secrets (section 5, plus the A6 checklist).
- [ ] Production has no dev seeder: run `infra/seed/production-bootstrap.sql` (the 4 role logins,
      the demo student and the 3 base consumables; password passed with `-v staff_password=…`,
      hashed by pgcrypto bcrypt, checked against the API login on 4 Oct), then
      `infra/seed/demo-data.sql`.
- [ ] Smoke-test: `/health`, `/swagger`, web login, one full workflow on the live URLs.
- [ ] Rebuild the APK with the live API URL and reinstall it.

### Task 6 — Full run-through (Sun 5 Oct)
- [ ] Happy path: student (mobile) submits → workflow → librarian approves (web) → mobile Approved
      → email arrives → QR check-in **inside the window** → booking completes → usage report shows it.
- [ ] Reject path and ask-for-change → edit and resend path.
- [ ] Failure cases: ineligible student (422), clashing slot, low-stock item, over budget, hostile
      objective, workflow failure (stop the agent → Failed with an error code), check-in too early
      (422), cancel then approve (409).
- [ ] Fix every bug found, each with a test.

### Documentation & evidence (Sun 5 Oct)
- [ ] README: live URLs, how to run locally, logins, architecture diagram, what's cut.
- [ ] 6 ADRs in `DOCS/adr/`: React state (Zustand), Flutter state (Provider), agent framework,
      **LLM = xAI Grok**, workflow state storage + background queue, **hosting = Railway**,
      email (Brevo + retry table). Add one for **D1 web session (refresh cookie)**.
- [ ] Swagger up to date; screenshots of every screen; CI green on `main`.
- [ ] Performance: k6, 50 concurrent users on `GET /api/rooms/available` → p50/p95/error rate.
- [ ] Demo video (10 min), final report, AI usage log, individual reflection, viva rehearsal.

### Mon 6 Oct: **submit**

---

## 5. Local setup (Docker + demo data)

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
holds a remote connection string — **don't load it into your shell for local work**. To test email
in compose, export the `Brevo__*` values from the root `.env` before `docker compose up` (the running
stack on 3 Oct had them empty).

Logins (all share the dev password in [`ACCOUNTS.md`](ACCOUNTS.md)): `student@`, `librarian@`,
`storeofficer@`, `admin@studyhive.dev`; eligibility cases `nimal@` (clean), `kavya@` (1 penalty,
eligible), `suspended@` and `penalty@` (ineligible). Audit test student:
`audit.web.20261003@studyhive.dev`. Until A2 merges the effective weekly limit is 2, so use a fresh
student for repeated end-to-end runs.

Audit test data in the dev DB is named `AUDIT …` (requests, room "AUDIT Room Z", equipment
"AUDIT Kit", consumable "AUDIT Glue sticks", supplier "AUDIT Supplies"); all non-request rows are
inactive. Delete or ignore before recording the demo.

### Deploy safety checklist (A6, before Task 5)

Before the first Railway deploy, and again after any change to the API service settings:

- [ ] API service variable `ASPNETCORE_ENVIRONMENT=Production` (the image's default; **never
      `Development`**, which would turn on the dev seed and its shared password).
- [ ] Fresh secrets generated for this deploy, never copied from `appsettings.Development.json`:
      `Jwt__SigningKey` (at least 32 bytes; the API refuses to start without it) and
      `Agent__InternalApiKey` (the same value as the agent's `INTERNAL_API_KEY`). For example,
      `openssl rand -base64 48`. Paste them into Railway only, never into a file in the repo.
- [ ] `appsettings.Development.json` is never mounted or copied into a deployed image.
      `api/.dockerignore` excludes it; check with
      `docker build -t studyhive-api:check api && docker run --rm --entrypoint ls studyhive-api:check /app`.
      The only settings file listed must be `appsettings.json` (checked 4 Oct 2026: only
      `appsettings.json`, environment `Production`, no `dev-only` values inside).
- [ ] `AuthCookie__SameSite` stays `None` with `AuthCookie__Secure=true` (the `appsettings.json`
      default), and `Cors__AllowedOrigins__0` is the web service's exact public URL (no `*`).
- [ ] After the deploy, signing in with `librarian@studyhive.dev` and the dev password must
      **fail**, which proves the dev seed did not run.

**LLM:** xAI Grok via `agent/app/llm.py` `chat(instructions, data)`, the only network seam. The LLM
never decides anything: it writes the Planner summary and the Validation revision note, with a
deterministic fallback on any failure. Tests monkeypatch it; CI never calls Grok.

---

## 6. Hosting: everything on Railway

One Railway project, four services.

| Service | Source | Notes |
|---|---|---|
| **Postgres** | Railway Postgres plugin | Needs extensions `citext` and `btree_gist` (the migration creates them). |
| **api** | `api/` (Dockerfile, .NET 8) | Public domain. Applies migrations on deploy. `/health` and `/swagger` must load. |
| **agent** | `agent/` (Dockerfile, Python 3.11 + uvicorn) | **No public domain**: reached only over private networking (`http://agent.railway.internal:8001`). |
| **web** | `web/` (Dockerfile: `npm run build` → static server) | Public domain. `VITE_API_BASE_URL` = the API's public URL, set at **build** time. |

Environment variables
- **api:** `ConnectionStrings__Default` (convert Railway's `postgresql://USER:PASS@HOST:PORT/DB` to `Host=HOST;Port=PORT;Database=DB;Username=USER;Password=PASS;SSL Mode=Require;Trust Server Certificate=true`), `Jwt__SigningKey` (≥ 32 bytes, **fresh — never the development value**), `Jwt__Issuer`, `Jwt__Audience`, `Agent__BaseUrl=http://agent.railway.internal:8001`, `Agent__InternalApiKey` (same as agent, **fresh**), `AllowedHosts`, `Cors__AllowedOrigins__0=<web URL>`, `ASPNETCORE_ENVIRONMENT=Production` (**never Development**), `CheckIn__OpensMinutesBefore=15`, `Brevo__ApiKey`, `Brevo__SenderEmail`, `Brevo__SenderName`.
- **agent:** `INTERNAL_API_KEY` (real secret), `ENVIRONMENT=production`, `GROK_API_KEY`, `GROK_BASE_URL`, `GROK_MODEL`. Bind to `0.0.0.0` and Railway's `PORT`.
- **web:** `VITE_API_BASE_URL`.
- **mobile APK:** `flutter build apk --release --dart-define=API_BASE_URL=<api URL>`.
- **D1 cookie:** web and API are on different Railway subdomains, so the refresh cookie must be
  `SameSite=None; Secure` in production (`Lax` locally) and CORS must allow credentials for the web
  origin only.

Before the demo: open every URL 5 minutes early; keep one completed workflow in the DB as a
fallback if Grok is rate-limited. Keep everything live until **21 October**.

---

## 7. Viva / evidence reminders
- Headline claims each have a test: **overlap rejected** (S2, exclusion constraint `no_double_booking` → 409), **last item can't be reserved twice** (S3, `chk_never_oversold`), **hostile objective changes nothing** (S4 golden case), **approval is one transaction** (S4, clash → 409 and nothing written), and after the fixes **cancelled requests cannot be approved** (A1) and **check-in only inside its window** (A3).
- We store tool inputs/outputs, validation results, timings, errors. We do **not** store chain-of-thought, raw prompts or API keys (say this in the report).
- The audit (`AUDIT.md`) and its fix-status table are evidence of testing and honest defect handling — mention it in the report.
- Keep the AI usage log and each person's reflection consistent with git history.
