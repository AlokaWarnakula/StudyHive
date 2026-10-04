# StudyHive pre-deploy functional audit

Audit date: 3 October 2026. This document records defects and unverified areas only; it does not
authorize or include production fixes.

## Codex — web dashboard and repository hygiene

### W-01 — staff sessions are lost on a full page reload

- **Where:** staff console authentication; `web/src/store/authStore.ts`.
- **Steps:** sign in as a staff user, then reload the page.
- **Expected:** the requested audit behaviour is that the staff session remains usable after the
  reload.
- **Actual:** the Zustand store has no persistence middleware and holds both access and refresh
  tokens only in memory. A reload resets them and sends the user back to sign-in. The source
  comments explicitly describe this as an accepted security trade-off.
- **Severity:** must-fix (the task explicitly requires reload persistence to be exercised).
- **Evidence:** `web/src/store/authStore.ts` lines 16–20; login page observed at
  `http://localhost:8081/login` on 3 Oct. Authenticated interaction remains unverified in this
  pass.

### W-25 — admin user management is not functional in production

- **Where:** Admin → Users; `web/src/pages/admin/UsersPage.tsx`,
  `web/src/dev/useFixture.ts`, `api/src/StudyHive.Api/Controllers/Admin/UsersController.cs`.
- **Steps:** sign in as admin and open `/users` in the production web build.
- **Expected:** list users, filter/search them, change a role with an audit reason, and activate or
  deactivate an account.
- **Actual:** production disables fixtures, so `UsersPage` renders **Not built yet**. The backing
  `GET /api/users`, `PUT /api/users/{id}/role`, and `PUT /api/users/{id}/status` handlers are all
  explicit `501 Not Implemented` scaffolds. The polished fixture controls can only appear in a
  development build and do not call the API.
- **Severity:** must-fix (either implement the feature or remove this route from the production
  navigation before deployment; the feature itself need not block deployment if it is explicitly
  out of scope).
- **Evidence:** API source lines 10–54; web source lines 22–33 and 44–152; unauthenticated
  `GET http://localhost:8080/api/users` returned 401, confirming the live API is running and
  protected (authenticated call intentionally not performed in this pass).

### W-26 — admin settings is not functional in production

- **Where:** Admin → Settings; `web/src/pages/admin/SettingsPage.tsx`,
  `web/src/dev/useFixture.ts`.
- **Steps:** sign in as admin and open `/settings` in the production web build; attempt **Save
  changes** or **Run a test workflow** in a development fixture build.
- **Expected:** settings must load and the displayed buttons must persist or execute their labeled
  actions.
- **Actual:** production disables fixtures and displays **Not built yet**. In the development-only
  fixture branch, **Save changes** and **Run a test workflow** have no event handlers, so they do
  nothing.
- **Severity:** must-fix (either implement the feature or remove this route from the production
  navigation before deployment; the feature itself need not block deployment if it is explicitly
  out of scope).
- **Evidence:** `web/src/pages/admin/SettingsPage.tsx` lines 19–25, 33–37, and 91–93;
  `web/src/dev/useFixture.ts` lines 21–38.

### W-01/W-13 — production visual assets are placeholders and no printable room QR exists

- **Where:** staff login and Rooms detail; `web/src/pages/auth/LoginPage.tsx`,
  `web/src/pages/rooms/RoomDetailPage.tsx`, `web/src/api/rooms.ts`,
  `api/src/StudyHive.Api/Controllers/Rooms/RoomsController.cs`.
- **Steps:** open the login screen, then inspect a room detail page as a permitted staff user.
- **Expected:** branded login imagery/logo, room photos where the UI calls for them, and a printable
  QR image on the room page (Task 4 step 3), not only the QR text.
- **Actual:** login renders `Placeholder label="library photograph"`; room detail renders the QR
  code as text and has no image/photo field, QR renderer, image endpoint, or print action. The API
  room model/controller only carries the QR string; repository search found no room photo/image
  field in API, web, or mobile live room models.
- **Severity:** must-fix (known acceptance gap; printable QR is required for physical check-in).
- **Evidence:** `LoginPage.tsx` lines 59–61; `RoomDetailPage.tsx` lines 94–97 and 114;
  `web/src/api/rooms.ts`; `RoomsController.cs`; browser login view on 3 Oct visibly presented the
  sign-in page with no branded image.

### M-14/API — check-in has no time-window enforcement (source confirmation)

- **Where:** `POST /api/room-bookings/{id}/check-in`;
  `api/src/StudyHive.Api/Controllers/Rooms/RoomBookingsController.cs`.
- **Steps:** submit a matching room QR for a confirmed booking days before its scheduled start.
- **Expected:** check-in is rejected until the advertised 15-minute pre-start window (and after an
  appropriate end/no-show boundary).
- **Actual:** after ownership, QR, and `Confirmed` checks, the controller immediately sets
  `CheckedInAt = DateTimeOffset.UtcNow`; it never compares the current time with `StartsAt` or
  `EndsAt`.
- **Severity:** blocker (a physical-room check-in can be recorded well before the reservation).
- **Evidence:** `RoomBookingsController.cs` lines 102–165. Live mutation was not performed because
  this audit is read-only.

### W-01 — web access tokens are not refreshed after expiry

- **Where:** web authentication and requests; `web/src/api/client.ts`, `web/src/store/authStore.ts`.
- **Steps:** sign in as any staff role, leave the dashboard open past the API's 30-minute access-token
  lifetime, then request a new page or action.
- **Expected:** a valid stored refresh token is exchanged once and the original request is retried,
  or the user receives a deliberate sign-in prompt.
- **Actual:** `apiFetch` sends the in-memory bearer token once, has no 401 handling, and every page
  calls it directly. The store does hold a refresh token, but exposes no refresh operation. After
  expiry, pages surface their ordinary request error or stale data until the user signs in again.
- **Severity:** must-fix (a staff workflow can silently fail during ordinary use).
- **Evidence:** `web/src/api/client.ts` lines 27–54 contains no refresh/retry branch; the mobile
  audit independently confirmed the same expiry pattern for its own client.

### Role-gate mismatch — Admin navigation links reach live 403 pages

- **Where:** Admin sidebar and the Requests, Maintenance, Reservations, and Suppliers routes;
  `web/src/App.tsx` and the corresponding controllers.
- **Steps:** sign in as Admin and select each of those visible sidebar links.
- **Expected:** a sidebar link is shown only where both the route and its API operations are allowed,
  or the page explains that it is read-only without issuing forbidden requests.
- **Actual:** Admin can see and navigate to all four routes, but each live page renders **Forbidden**
  for its primary data call. For example, the stock-reservations read endpoint permits StoreOfficer
  and Librarian, while the UI route includes Admin.
- **Severity:** must-fix (role gates are inconsistent and produce broken visible navigation).
- **Evidence:** authenticated Admin walkthrough on 3 Oct: `/requests`, `/maintenance`,
  `/reservations`, and `/suppliers` each visibly rendered **Forbidden**. Controller role attributes
  confirm the mismatch; no mutation was attempted.

### Deployment precondition — committed development credentials must never reach Railway

- **Where:** `api/src/StudyHive.Api/appsettings.Development.json`, `docker-compose.yml`, deployment
  configuration.
- **Actual:** the repository tracks development `SigningKey` and `InternalApiKey` values and the
  local compose API explicitly runs with `ASPNETCORE_ENVIRONMENT=Development` while mounting that
  file. This invalidates the earlier claim that no development credentials are tracked. The compose
  comments and API Dockerfile describe the intended boundary: the development file is mounted only
  locally and production must supply real environment variables.
- **Severity:** must-fix deployment gate, not a confirmed production leak.
- **Evidence:** tracked-file and configuration-presence checks on 3 Oct; `docker-compose.yml` API
  environment and read-only development-settings mount. Verify Railway uses Production and injects
  unique SigningKey/InternalApiKey values before deploy; never package or mount the development file.

### Code hygiene — stale scaffold labels obscure implemented work

- **Where:** `web/src/api/users.ts`, `mobile/lib/api/quotations_api.dart`.
- **Steps:** inspect comments beside live client calls.
- **Expected:** comments accurately state whether endpoints are unavailable.
- **Actual:** both files retain `SCAFFOLD`/`TODO` text claiming their calls return 501 or are missing.
  For Users this matches the live controller; for quotations the client actively calls a completed
  S4 route, so the comment is stale and makes the audit surface misleading.
- **Severity:** nice-to-have.
- **Evidence:** `web/src/api/users.ts` lines 1–12 and 49–62; `mobile/lib/api/quotations_api.dart`
  lines 1–24 and 52–53.

### Verified hygiene checks

- `docker compose ps`: API, agent, database, and web services were up; agent and database were
  healthy. `GET /health` returned 200 with `{"status":"healthy"}` and `GET /login` returned 200.
- No `console.log`, `console.debug`, Dart `print`, or `debugPrint` calls were found in production
  API/agent/web/mobile source paths.
- No tracked `.env`, PEM/key, or certificate files were found. **Correction:**
  `appsettings.Development.json` is intentionally tracked and contains development signing/internal
  credentials for the local stack; see the deployment precondition above. The agent and API Docker
  ignore rules exclude those development settings from the built image.

### Not yet exercised by this audit pass

- Authenticated navigation for Librarian (Dashboard, Approvals, Requests, Students, Rooms,
  Equipment, Maintenance, Reports, Workflow runs), StoreOfficer (Dashboard, Consumables,
  Reservations, Suppliers, Consumable usage) and Admin (Dashboard plus all displayed links) was
  exercised on the local stack. The reload test consistently returned the user to `/login`.
  Forms, filters/sorts, dialogs, destructive actions, and CRUD mutations were intentionally not
  submitted in this read-only audit; their validation/error paths remain only partially exercised.
- Real device rendering and end-to-end email/check-in/usage paths are assigned to the mobile/API
  audit section that follows.

## Claude — mobile app, API contracts and end-to-end workflow

Audited 3 October 2026 on branch `mobile/android` (`bad0b4c`) against the running compose stack
(API :8080, agent, `studyhive-db`) and the debug APK on the Pixel_8_Pro emulator, signed in as
`emu.tester.20261003@studyhive.dev`. End-to-end paths were driven through the API as
`student@studyhive.dev`, `nimal@studyhive.dev`, `suspended@studyhive.dev` and `librarian@studyhive.dev`.
Screenshots (`s0`–`s9.png`) are in the Claude session scratchpad
`%LOCALAPPDATA%\Temp\claude\C--Users-aloka-Mine-Projects-StudyHive\c573187b-44a3-45e8-97f7-6c89bcef42e2\scratchpad\`.
No source code was changed.

**Test data this audit created in the dev DB** (safe to keep, or delete before a demo):
`AUDIT A cancel then approve` (`1eab1811…`, now Approved + checked in, Quiet Study 101 on 8 Oct),
`AUDIT B reject path` (`d77bc511…`, Rejected), `AUDIT C revision path` as student (`7253d67d…`,
auto-rejected INELIGIBLE) and as nimal (`90b2c466…`, RevisionRequested), `AUDIT D orphan draft` for
suspended (cancelled). Two `BookingApproved` emails were queued (not sent, see C-24).

### Blockers

#### C-01 — approving a cancelled request brings it back as Approved (API)

- **Where:** `POST /api/approvals`; `api/src/StudyHive.Api/Controllers/Approvals/ApprovalsController.cs`
  (`Create` / `ApproveAsync`), `BookingRequestsController.Cancel`.
- **Steps:** student submits a request → it reaches PendingApproval → student cancels it
  (`DELETE /api/booking-requests/{id}`, 204) → librarian approves its quotation.
- **Expected:** the cancelled request leaves the approval queue, or approval is refused with 409.
- **Actual:** cancel only sets the request status; the quotation stays `Proposed` and stays in
  `GET /api/approvals?status=Pending`. `ApproveAsync` checks only the quotation status, so approval
  returns **201**, flips the request from Cancelled back to **Approved**, books the room
  (`room_bookings` row Confirmed, Quiet Study 101, 8 Oct 04:30Z) and queues a `BookingApproved` email.
- **Severity:** blocker — a student's cancellation is silently overridden and a room is held.
- **Evidence:** request `1eab1811-8226-4a70-8b61-880471e85df3`, quotation `24c7efb2…`: cancel 204 →
  status Cancelled → still in pending queue: true → approve 201 → status Approved.

#### C-02 — "ask for change" is a dead end for the student

- **Where:** `PUT /api/booking-requests/{id}`, `POST /api/booking-requests/{id}/submit`
  (`BookingRequestsController.Update` / `Submit`); mobile `booking_detail_screen.dart`.
- **Steps:** librarian decides `RevisionRequested` with a comment → student tries to edit and resubmit.
- **Expected:** the student can change the request and send it again (that is the point of the
  decision; the email template `BookingRevisionRequested` tells them to).
- **Actual:** both endpoints accept only `Draft`: edit → **409** "Only draft requests can be edited";
  submit → **409** "Only draft requests can be submitted". The app offers only "Cancel booking" for
  RevisionRequested. The request can never progress; the student must start a new one.
- **Severity:** blocker — one of the three required end-to-end paths cannot be completed.
- **Evidence:** request `90b2c466-d5a9-4070-960b-a8eb1b900ad9` (nimal): revise 201 → status
  RevisionRequested with the comment → PUT 409, submit 409.

#### C-03 — weekly limit is off by one: the 3rd booking of the week is accepted, then auto-rejected

- **Where:** `BookingRequestsController.Submit`, `Services/BookingEligibilityService.cs` lines 77–85,
  `Services/WorkflowOrchestrationService.cs` (planner input `StudentEligible`).
- **Steps:** a student with 2 workflow submissions this week submits a 3rd request (limit 3).
- **Expected:** the 3rd is allowed; the 4th is refused at submit with 422.
- **Actual:** `Submit` evaluates eligibility *before* `StartAsync` inserts the new
  `workflow_executions` row (2 < 3 → 202 Accepted). The workflow then re-evaluates eligibility, now
  counting its own row (3 ≥ 3), and ends **Rejected / INELIGIBLE "Weekly booking limit reached
  (3 per week)."** The effective limit is 2, and the student sees an accepted request turn Rejected.
- **Severity:** blocker — core business rule wrong and visible in any 3-booking demo.
- **Evidence:** request `7253d67d-694f-43bc-8fd1-0170b3fb2277` (student): submit 202 → workflow
  Rejected, error_code INELIGIBLE. The emulator user is at "2 of 3", so their next request will hit this.

#### C-04 — check-in has no time window (live confirmation of M-14/API above)

- **Where:** `POST /api/room-bookings/{id}/check-in`, `RoomBookingsController.CheckIn` lines 103–165;
  mobile booking detail "Opens 15 min before".
- **Steps:** check in to a confirmed booking days before it starts.
- **Expected:** refused outside [start − 15 min, end] (PLAN.md Task 6b).
- **Actual:** **200**, `checked_in_at` written. Repeating returns 200 and keeps the first time.
- **Severity:** blocker (agree with Codex).
- **Evidence:** emulator booking `ec71232a…` checked in 2026-10-03 14:13Z for a slot starting
  2026-10-06 08:30Z; audit request A checked in 14:38Z for a slot starting 2026-10-08 04:30Z.
  Wrong code via request id → 404 (see C-12); another student → 403 (correct).

#### C-05 — the mobile app stops working 30 minutes after sign-in (no token refresh)

- **Where:** `mobile/lib/api/api_client.dart` (`_send` has no 401 handling),
  `mobile/lib/state/auth_provider.dart` (`/api/auth/refresh` is called only by `tryRestoreSession` at
  app start); API `JwtOptions.AccessTokenMinutes = 30`, `ClockSkew` 30 s (`Program.cs` line 101).
- **Steps:** sign in (or start the app), keep it open for more than 30 minutes, open a booking.
- **Expected:** the app silently exchanges its stored refresh token (valid 14 days) and retries.
- **Actual:** every call returns 401. Booking detail shows **"Request failed with status 401"**; list
  screens keep their old data and show nothing, because `TrackScreen` only shows an error when the
  list is empty. The only recovery is killing and reopening the app. Any demo or real use longer than
  30 minutes hits this.
- **Severity:** blocker.
- **Evidence:** `s12.png` at 14:50Z; the newest Emu refresh token was issued 14:19:04Z (access token
  valid until 14:49:34Z with skew). A login response confirms `accessTokenExpiresAt` = issue + 30 min.

### Must-fix

#### C-06 — after check-in the app still offers check-in and cancel; nothing ever completes a booking

- **Where:** mobile `booking_detail_screen.dart` (`_timeline`, buttons keyed only on
  `request.status == 'Approved'`), `track_screen.dart` `_BookingTile`, `home_screen.dart` next-booking
  card; API `BookingRequestResponse`.
- **Steps:** check in (C-04), return to Home, Bookings and the booking detail; pull to refresh.
- **Expected:** a "Checked in" state with no check-in or cancel buttons; after the slot ends the
  booking moves to Past.
- **Actual:** confirmed the known finding — detail still shows "Check in at the room · Opens 15 min
  before", "Check in with QR" and "Cancel booking"; Home and Bookings › Active still show "Check in".
  The root cause is wider: `BookingRequestResponse` carries no room booking or `checkedInAt`, and no
  code path ever sets a request to `Completed` or a room booking to `Completed`/`NoShow` (only the dev
  seeder uses `Completed`). Approved bookings stay "Active" forever and never reach "Past".
- **Severity:** must-fix.
- **Evidence:** `s7.png` (detail after check-in), `s2.png`; DB `room_bookings` `ec71232a…` has
  `checked_in_at` set and status Confirmed; request A is still `Approved` after check-in.

#### C-07 — "Cancel booking" is shown on approved bookings but the API refuses it

- **Where:** `booking_detail_screen.dart` (button shown for `_cancellableStatuses` **or** Approved);
  `BookingRequestsController.CancellableStatuses` excludes Approved.
- **Steps:** open an approved booking and tap Cancel booking (there is no confirmation dialog).
- **Expected:** either the booking is cancelled and its room/stock released, or there is no button.
- **Actual:** `DELETE` → **409** "This request is already 'Approved'.", shown as a snackbar. No API
  path exists to cancel an approved booking and release its room and reserved stock.
- **Severity:** must-fix (decide: implement approved-cancel with release, or hide the button).
- **Evidence:** request A after approval: DELETE 409 with that detail.

#### C-08 — tabs never reload, so Home/Bookings go stale (known finding, confirmed)

- **Where:** `home_screen.dart` — `IndexedStack` keeps all four tabs alive; `_HomeDashboard` and
  `TrackScreen` refresh only in `initState`. Switching tabs or returning from a detail screen never
  refreshes.
- **Expected:** data refreshes on tab switch, on return from a detail screen and on app resume.
- **Actual:** only a manual pull-to-refresh updates them. On the emulator Home showed "1 of 3" and no
  waiting request; a pull then showed "2 of 3" and the PendingApproval "Manual room code check-in
  test" (`s0.png` vs `s1.png`).
- **Severity:** must-fix.

#### C-09 — the student's room and time choice is thrown away

- **Where:** `room_detail_screen.dart` line 121 ("Book this room"), `room_schedule_screen.dart`
  around line 104 ("Use <slot>"); `CreateBookingRequestRequest` has no room field.
- **Steps:** Rooms → A-101 → Book this room; or See free times → pick a slot → Use slot.
- **Expected:** the request form opens pre-filled with that room/date/time, and the room is honoured
  or at least sent as a preference.
- **Actual:** both push a blank `const CreateRequestScreen()`. The request has no room, so the agent
  picks one (every audit request got the free Quiet Study 101). Today's past slots are also listed
  and selectable as "Free" (`s5.png`: at 20:10 local the 8 AM–6 PM slots were selectable).
- **Severity:** must-fix (misleading main booking path).

#### C-10 — room QR codes are visible to students, so check-in proves nothing

- **Where:** `GET /api/rooms` and `GET /api/rooms/{id}` return `qrCode` to Student tokens; mobile room
  detail shows "QR code STUDYHIVE-A-101"; the QR screen offers "Enter room code instead".
- **Steps:** as a student, open any room detail, then type that code into "Enter room code instead".
- **Expected:** the code can only be obtained at the door.
- **Actual:** the student API response includes `qrCode: STUDYHIVE-A-101` and the screen displays it
  (`s4.png`). Combined with C-04, a student can check in from anywhere at any time.
- **Severity:** must-fix (integrity of check-in and usage data).

#### C-11 — a failed submit leaves an orphan Draft the app cannot submit

- **Where:** `booking_requests_provider.dart` `createAndSubmit` (create, then submit, as two calls);
  `track_screen.dart` lists Draft under Waiting with no submit or edit action.
- **Steps:** send a request while ineligible (suspended, over the limit) or rate-limited.
- **Expected:** nothing is left behind, or the Draft can be fixed and resent.
- **Actual:** create 201, submit **422** "Student is suspended until 2026-10-29."; the Draft remains,
  and each retry adds another. The app offers only Cancel on it.
- **Severity:** must-fix.
- **Evidence:** `suspended@studyhive.dev`, request "AUDIT D orphan draft" (cancelled afterwards).

#### C-12 — API error bodies are lost, so students see generic or empty messages

- **Where:** `mobile/lib/api/api_client.dart` (`ApiException` keeps only `title`/`detail`);
  `RoomBookingsController.CheckIn` lookup by request id.
- **Actual:** ASP.NET validation problems carry the reason in `errors` with no `detail`, so the app
  shows "One or more validation errors occurred." (for example `{"Password":["…minimum length of
  '8'"]}` or `{"PreferredTimeTo":["…must be after the start time."]}`). A wrong room code sent with the
  booking-request id returns **404 with no detail**, not the documented 422 "Invalid room QR code",
  because the fallback query filters on the code; the QR screen then shows "Not Found".
- **Severity:** must-fix.

#### C-13 — inert controls, and no notifications feature

- **Where:** `home_screen.dart` line 51 (Notifications bell) and line 68 (Rooms search),
  `onPressed: () {}`; `login_screen.dart` line 126 "Forgot password", `onPressed: () {}`;
  `profile_screen.dart` lines 150–154: the Notifications, Change password and Help rows have no `onTap`
  but show chevrons.
- **Actual:** tapping does nothing. There is no notifications screen and no API for in-app
  notifications, password change or password reset (checked the full route list). The task's
  "notifications" item therefore cannot be tested: beyond email, it does not exist.
- **Severity:** must-fix (hide or implement before deploy).

#### C-14 — booking screens show the request, not the booking

- **Where:** `booking_detail_screen.dart`, `track_screen.dart`, `quotation_view_screen.dart`.
- **Actual:** the detail and list show the *preferred* date/time and the *budget*. The assigned room
  (Quiet Study 101) appears only inside Cost breakdown, as a raw ISO string
  "2026-10-06T14:00:00+05:30 · 2 h" (`s8.png`). Cost breakdown's header reads **PROPOSED** above an
  "Approved" tag. The timeline has no "Approved by librarian" step.
- **Severity:** must-fix (a student cannot see where to go).

#### C-15 — room-usage report buckets hours in UTC and never counts no-shows

- **Where:** `GET /api/reports/room-usage`, `ReportsController.cs` lines 101–160.
- **Actual:** `bookingsByHour` uses UTC hours, so 10:00 and 14:00 Colombo bookings land in hours 4
  and 8. `noShows` is always 0 because nothing sets `NoShow` (C-06), and check-ins are not reported at
  all, so the check-in → usage-report step of the end-to-end flow has no visible effect.
- **Severity:** must-fix.
- **Evidence:** room-usage for 1–31 Oct: `bookingsByHour` non-zero at hours 3, 4 and 8; `noShows: 0`.

### Nice-to-have

- **C-16 — weekly allowance shown wrongly.** Home/Profile "Bookings this week" counts every
  non-terminal request from any week (`home_screen.dart` `used`), while the server counts this Colombo
  week's workflow submissions, rejected ones included. The two disagree (the dev student would show
  "5 of 3").
- **C-17 — "Free now" is invented.** `models/room.dart` line 30 shows "Free now" for every active
  room without any availability check (`s4.png`).
- **C-18 — "Good morning" is hard-coded** (`home_screen.dart` line 44); it was showing at 20:00.
- **C-19 — the room detail header draws under the status bar** (`s4.png`).
- **C-20 — Approval status and Cost breakdown push each other**, growing the back stack.
- **C-21 — placeholders (known findings, confirmed):** room "PHOTO" / "ROOM PHOTO", profile "AVATAR",
  login `Ph(label: 'logo')` (`login_screen.dart` line 73). No room image field exists in the API, the
  DB (`study_rooms` has none) or the mobile model. Launcher icon and `launch_background.xml` are the
  `flutter create` defaults, and no icon/splash package is in `pubspec.yaml`; a cold start shows the
  Flutter logo on black (`splash.png`).
- **C-22 — stale scaffold comments** in `mobile/lib/api/quotations_api.dart` and
  `state/quotations_provider.dart` (agree with Codex).
- **C-23 — the emulator build is a debug APK** (`DEBUGGABLE` flag). `demoPreviewEnabled` defaults to
  `kDebugMode`, so preview records can appear on screens opened without live data. Use a release
  build, or `--dart-define=ENABLE_DEMO_DATA=false`, for the final run-through.

### Environment notes (not code defects)

- **C-24 — email is queued, not sent, in this stack.** `Brevo__ApiKey` and `Brevo__SenderEmail` are
  empty in `studyhive-api` (length 0), and the API logs "Email sender off". Approve, reject and
  revise each queue one `email_notifications` row correctly (Emu's `BookingApproved` has been Queued
  since 14:04Z with 0 attempts). Actual delivery was **not tested** here; it was verified on 1 Oct
  (there is a `Sent` row in the table).
- Quotation totals of Rs. 0 are correct: Quiet Study 101's seeded `hourly_rate` is 0.

### Verified working

Login (a bad password gets 401 with a clear message; staff accounts are refused by the app),
register (duplicate email gets 409 with a clear message; client-side 8-character rule), session
restore, browse rooms with capacity and equipment filters, room detail and schedule (booked slots from
`/schedule` are shown correctly), the 3-step request form with consumables, workflow progress polling,
the approve and reject paths (request status, decision comment visible on Approval status, reservation
and email rows), check-in ownership (403 for another student), and repeated check-in idempotence.

### Not tested, and why

- **Sign out / sign in / register on the device:** I could not find the Emu account's password
  recorded anywhere, and signing out would lose the session needed for the pending manual-code
  check-in test. Covered at API level only.
- **Camera QR scan:** nothing was scanned (no physical code in front of the emulator camera). The scan
  path calls the same endpoint as manual entry.
- **Manual-code check-in on the device for "Manual room code check-in test":** still PendingApproval;
  left for the librarian step as planned.
- **Email delivery:** sender off (C-24).
- **Consumable "use" after check-in and the consumable-usage report:** stock reservations stay
  `Reserved`; marking them Used is a staff action not exercised here.

## Claude — independent web audit (second pass, at the user's request)

Audited 3 October 2026, 15:05–15:40Z, in the built-in browser against `http://localhost:8081`
(compose stack, branch `mobile/android`), as librarian, store officer and admin. Unlike the first
pass, every form and action button was **submitted** — on test data only. A `fetch` logger recorded
the status code of every API call each page made; those codes are the evidence below. No source code
was changed.

**Test data this pass created** (all marked AUDIT; deactivated where it could affect real use):
requests "AUDIT W approve via web" (`285c332c…`, Approved, markers issued), "AUDIT W ask-for-change
via web" (`503b87bc…`), "AUDIT W reject via web" (`c2dbedff…`), "AUDIT W cancel with items"
(`4b4b1e4c…`, Cancelled); student `audit.web.20261003@studyhive.dev` (dev password from
ACCOUNTS.md); room "AUDIT Room Z" (inactive) with one maintenance window on 20 Oct; equipment
"AUDIT Kit" (inactive); consumable "AUDIT Glue sticks" (deactivated); supplier "AUDIT Supplies"
(inactive). The user's "Manual room code check-in test" request was left untouched.

### Codex findings re-checked live

| Codex finding | Result |
|---|---|
| W-01 reload signs staff out | **Confirmed** for all three roles. Also: after signing in again the user lands on `/`, not the page they asked for. |
| Web tokens never refreshed | **Confirmed in code** (`web/src/api/client.ts` has no 401 branch). Not waited out live. |
| Admin links reach 403 pages | **Confirmed**: `/requests`, `/maintenance`, `/reservations`, `/suppliers` each call an API that returns **403** and render "Forbidden". More mismatches in CW-05. |
| W-25 / W-26 Users and Settings | **Confirmed**: both render "Not available yet"; `GET /api/users` as admin → 501. |
| Printable room QR missing | **Confirmed**: room detail shows `STUDYHIVE-QUIET-101` as text only. |
| Committed development keys | **Confirmed**: `appsettings.Development.json` is tracked and holds the SigningKey and InternalApiKey. |

### Must-fix

#### CW-01 — form errors in the Rooms area appear behind the open dialog, so Save "does nothing"

- **Where:** `web/src/pages/rooms/RoomsPage.tsx` (Add room), `RoomDetailPage.tsx` (Edit room, Add
  equipment), `EquipmentPage.tsx` (Add/Edit equipment). Each sets a page-level `error` that renders
  outside `<Dialog>`. The store pages render errors inside the dialog and are fine.
- **Steps:** Rooms → Add room → Save room with empty fields; or enter an existing QR code.
- **Expected:** the reason appears in the dialog.
- **Actual:** empty form: no API call and no visible message — the text "Enter a room name, building
  and QR code…" is in the page *behind* the modal. Duplicate QR: `POST /api/rooms` → **400**, again
  shown only behind the modal.
- **Severity:** must-fix.

#### CW-02 — the web also throws away the API's field errors

- **Where:** `web/src/api/client.ts` `ApiError` message uses `detail ?? title`; pages show `.message`.
- **Actual:** duplicate room QR → API says `{"QrCode":["A room with this QR code already exists."]}`,
  user sees "One or more validation errors occurred.". Admin sets a student's weekly limit to 0 →
  `PUT /api/student-profiles/{id}` **400**, same generic text. Same root cause as mobile C-12; fix
  both together.
- **Severity:** must-fix.

#### CW-03 — Sign out and the signed-in user are hidden on 18 pages

- **Where:** `showUser={false}` in 18 page files (Rooms, Room detail, Room calendar, Equipment,
  Reports ×3, Request detail, Review proposal, Quotation detail, Workflow run, Audit log, Users,
  Settings, Consumables, Consumable detail, Low stock, Suppliers).
- **Actual:** on those pages there is no Sign out button and no indication of who is signed in. A
  store officer can sign out only from Dashboard or Reservations.
- **Expected:** the shell shows the user and Sign out on every page.
- **Severity:** must-fix.

#### CW-04 — the audit log records only approval decisions

- **Where:** only `ApprovalsController` writes `audit_logs`; the Audit log page is subtitled
  "Read-only record of every change" and Settings says "Changes are written to the audit log".
- **Actual:** the table holds only `QuotationApproved`, `QuotationRejected`, `RevisionRequested`.
  This pass created/edited rooms, equipment, a maintenance window, a consumable, stock-in, a supplier,
  a student profile and issued a reservation — none were logged. Audit-log filters themselves work
  (`action=QuotationRejected` → 2 rows).
- **Severity:** must-fix (either log the staff writes or stop claiming "every change").

#### CW-05 — role gates and API roles disagree in more places than the four 403 pages

- **Where:** `web/src/App.tsx` role lists vs controller `[Authorize]` attributes.
- **Actual (all observed live or read from the attributes):**
  - Admin sees **"Schedule a window"** and **Room detail → Schedule maintenance**, but
    `MaintenanceWindowsController` is Librarian-only (list → 403).
  - Admin sees **Add supplier** and the API allows the POST, but listing suppliers → 403, so an admin
    can create a supplier and never see it.
  - Admin consumable detail: `GET /api/stock-transactions` → **403**.
  - Librarians may read stock reservations (`GET` allows Librarian) but have no page for them.
  - `DELETE /api/rooms/{id}` and `DELETE /api/consumables/{id}` (Admin-only soft-deactivate) have no
    button anywhere. A consumable cannot be retired from the web at all: `PUT` ignores `isActive`
    (sent `false`, response `isActive: true`).
- **Expected:** one role table, shared by routes, nav, buttons and API.
- **Severity:** must-fix (merge with Codex's role-gate finding).

#### CW-06 — maintenance windows cannot be changed, and they silently overlap confirmed bookings

- **Where:** `MaintenanceWindowsController` (POST and GET only), `MaintenancePage.tsx`.
- **Actual:** no edit or cancel in the API or UI — a mistyped window is permanent. A window that
  overlaps confirmed bookings is accepted; the bookings are only counted ("Bookings hit"), stay
  Confirmed, and the students are not told. Client validation (required fields, end after start) works.
- **Severity:** must-fix.

#### CW-07 — reservations: stuck Pending rows, a report that misses releases, no context

- **Where:** `BookingRequestsController.Cancel`, `ReportsController` consumable-usage (lines ~200–236),
  `ReservationsPage.tsx`.
- **Actual:**
  - A student cancelling a PendingApproval request with items leaves its reservation **Pending** and
    its quotation **Proposed** forever (request `4b4b1e4c…`, 3 markers). Extends C-01.
  - The consumable-usage report counts "released" from `stock_transactions`, but releasing a Pending
    reservation writes no transaction: two reservations released today by reject/ask-for-change show
    as **Released 0**.
  - The Reservations table has no student, booking, room or date, so the store officer cannot tell
    what an item is for or when to issue it. Issue works (`PUT …/use` 200 → Issued).
- **Severity:** must-fix.

#### CW-08 — the Students page cannot identify or correctly describe a student

- **Where:** `web/src/pages/requests/StudentsPage.tsx`, `GET /api/student-profiles`.
- **Actual:** no name or email column — only a student number. The Status column shows **Active**
  for `suspended@studyhive.dev`, who is suspended until 29 Oct. The admin panel edits only the weekly
  limit and an Active/Suspended toggle: no penalty points, no suspension end date. Saving a valid
  limit works (PUT 200, list updates).
- **Severity:** must-fix.

### Nice-to-have

- **CW-09 — signed-in users are sent to the sign-in page.** Any route the role may not open, and any
  unknown URL (`path="*"`), redirects to `/login` while the user is still signed in (store officer
  → `/reports`, `/approvals`, `/rooms`). Show "not allowed" / 404 inside the shell instead.
- **CW-10 — request detail leftovers.** Requested items show as raw GUIDs
  (`30000000-0000-0000-0000-000000000001 · 2`); the page still says "Stock levels and reservation state
  come from the S3 consumables API, which is not built yet" (`RequestDetailPage.tsx` line 185 — it is
  built); the status timeline is a fixed list, not the request's history; an unknown id shows a bare
  "Not Found" with no way back.
- **CW-11 — raw timestamps.** Quotation and review pages show room lines as
  "Quiet Study 101 2026-10-13T10:00:00+05:30" (same as mobile C-14); Requests table shows
  "10:00:00 – 12:00:00".
- **CW-12 — no staff view of check-ins.** Room calendar shows one room at a time with no booker,
  purpose or check-in state; no staff page shows whether a booking was checked in. Pairs with C-06.
- **CW-13 — irreversible actions have no confirmation:** Approve, Reject, Ask for a change, Issue,
  Release, Remove equipment all fire on the first click (decision buttons are disabled while pending,
  so double-submit is handled).
- **CW-14 — suppliers are an unlinked address book.** `consumable_suppliers` has 9 seeded rows but
  no controller or screen uses it; Stock in has no supplier field. Supplier form validates one field at
  a time and does not mark Phone as required.

### Verified working (submitted live)

Login errors (wrong password; student account refused with a clear message); dashboards for all three
roles; librarian Approve / Ask for a change / Reject with the required-comment check (201 each; DB:
room Confirmed, 2 markers Reserved, `BookingApproved` email queued, audit row written; rejected and
revised reservations Released); approvals, requests and workflow filters, sort and Search-button
search with clear empty states; workflow run detail; quotation detail; Rooms add/edit, equipment
assign/remove, equipment filter; Equipment add/edit/deactivate; Maintenance create with validation;
room calendar week navigation and room picker; Consumables add (with validation), edit (negative price
blocked), stock-in (with validation); Low stock; Suppliers add/edit/deactivate with email validation;
Reservation Issue; consumable-usage and booking reports; admin student limit edit; audit-log filters;
Sign out (`POST /api/auth/logout` 204, protected routes then redirect to `/login`); no horizontal
scroll at 375 px.

### Not tested, and why

- **Web 30-minute token expiry live:** not waited out; code evidence only.
- **Loading and API-down error states:** stopping the API would disrupt the shared stack.
- **Reservation Release button:** the only held reservations belong to real test bookings; Release
  was exercised indirectly through reject and ask-for-change.
- **Agent code hygiene:** not re-checked; Codex's TODO/debug-log scan was spot-checked only for
  web/api/mobile (16 TODO/SCAFFOLD markers, all in Users, quotations scaffolds and CW-10).

## Fix status

Fixes for the findings above (PLAN.md section 3). One row per audit ID; "cut" rows say why.

| Audit ID | Fixed in PR | Test |
|---|---|---|
| C-01 | [#27](https://github.com/ItsAloka/StudyHive/pull/27) | `BookingLifecycleTests.Approving_A_Request_The_Student_Cancelled_Returns_409_And_Books_Nothing`, `…Request_Is_No_Longer_Pending_Returns_409_request_not_pending`, `BookingRequestsControllerTests.A_Request_Cancelled_While_The_Agents_Run_Never_Reaches_PendingApproval`, `…Cancelled_Before_Its_Workflow_Is_Dequeued_Is_Never_Processed`, `…Cancelled_During_A_Failing_Validation_Stays_Cancelled_And_Gets_No_Email`; live: cancel 204 → approve 409 |
| CW-07 (stuck Pending reservations) | [#27](https://github.com/ItsAloka/StudyHive/pull/27) | `BookingLifecycleTests.Cancelling_A_PendingApproval_Request_Supersedes_Its_Quotation_Releases_Stock_And_Stops_The_Workflow` |
| C-07 (API) | [#27](https://github.com/ItsAloka/StudyHive/pull/27) | `BookingLifecycleTests.Cancelling_An_Approved_Booking_Before_It_Starts_…`, `…After_It_Started_Returns_409_…`, `EmailSenderTests.A_Cancelled_Email_Lists_The_Released_Room_Times` |
| C-06 (API) | [#27](https://github.com/ItsAloka/StudyHive/pull/27) | `BookingLifecycleTests.Ended_Bookings_Close_As_Completed_Or_NoShow_And_The_Request_Completes`, `…The_Request_Response_Carries_Its_Room_Bookings` |
| C-15 (no-shows) | [#27](https://github.com/ItsAloka/StudyHive/pull/27) | `BookingLifecycleTests.Ended_Bookings_Close_As_Completed_Or_NoShow_And_The_Request_Completes` |
| C-03 | [#28](https://github.com/ItsAloka/StudyHive/pull/28) | `EligibilityAndRevisionTests.The_Third_Request_Of_The_Week_Reaches_PendingApproval_And_The_Fourth_Is_Refused_At_Submit`; live: requests 1–3 PendingApproval, 4th → 422 |
| C-02 (API) | [#28](https://github.com/ItsAloka/StudyHive/pull/28) | `EligibilityAndRevisionTests.A_Revision_Can_Be_Resent_As_Quotation_Version_2_Without_Using_Another_Weekly_Slot` (edit / resend as is), `…Only_Draft_Or_RevisionRequested_Requests_Can_Be_Edited_Or_Submitted`, `…Two_Simultaneous_Submits_Of_One_Request_Start_Exactly_One_Workflow`, `…A_Cancel_And_An_Edit_At_The_Same_Time_Never_Leave_The_Request_Editable`; live: revise → PUT 200 → submit 202 → quotation v2 |
| C-04 | [#29](https://github.com/ItsAloka/StudyHive/pull/29) | `RoomCheckInAndUsageReportTests.Check_In_Outside_The_Window_Is_422` (early / ended), `…Check_In_Opens_Fifteen_Minutes_Before_The_Start`; live: 9 days early → 422 outside-check-in-window |
| C-10 | [#29](https://github.com/ItsAloka/StudyHive/pull/29) | `RoomCheckInAndUsageReportTests.Students_Never_Receive_Room_Qr_Codes_But_Staff_Do`; mobile `rooms_api_test` null qrCode; live: student qrCode null |
| C-12 (check-in) | [#29](https://github.com/ItsAloka/StudyHive/pull/29) | `RoomCheckInAndUsageReportTests.A_Wrong_Code_Sent_With_The_Booking_Request_Id_Is_422_Not_404`; live: 422 "Invalid room QR code" |
| C-05 | [#30](https://github.com/ItsAloka/StudyHive/pull/30) | mobile `session_refresh_test`: a 401 refreshes once and retries, concurrent 401s share one refresh, a second 401 is not retried, a rejected refresh signs out with a reason, a 429/500 from refresh keeps the session, signing out during a slow refresh stays signed out, an offline cold start keeps the stored session; live (emulator, 2-minute tokens): booking detail loads after expiry, refresh token rotated |
| C-12 (mobile) | [#30](https://github.com/ItsAloka/StudyHive/pull/30) | mobile `session_refresh_test`: a validation problem shows its first field message, a slow server times out with a friendly message |
| W-01 (API cookie part) | [#31](https://github.com/ItsAloka/StudyHive/pull/31) | `AuthCookieTests` (cookie flags + no body token, rotation/replay 401, missing cookie 401, form post 415, logout clears, mobile flow unchanged, CORS credentials only for allowed origins); live: login/refresh/logout via cookie 200/200/204, refresh after logout 401. Web client part follows in the A5 web PR |
| W-01 (web: reload, refresh on 401) | [#32](https://github.com/ItsAloka/StudyHive/pull/32) | web `client.test.ts` (refresh + retry, one shared refresh, no second retry, rejected refresh → "session expired", 429/500 keep the session, sign-out during refresh stays signed out), `SessionRestore.test.tsx` (reload restores the session); live: full reload of /requests stays signed in, cookie not readable from JS, no web storage used |
| CW-02 | [#32](https://github.com/ItsAloka/StudyHive/pull/32) | web `client.test.ts` (first field error when no detail, detail wins, 15 s timeout, offline message); live: duplicate room QR shows "A room with this QR code already exists." |
| CW-09 (deep link part) | [#32](https://github.com/ItsAloka/StudyHive/pull/32) | `SessionRestore.test.tsx` (deep link survives sign-in, `safeNextPath` rejects `//host`, absolute URLs and backslashes); live: /requests → /login?next=%2Frequests → back to /requests. Wrong-role handling stays in web polish |
| Committed development keys (deployment precondition) | [#33](https://github.com/ItsAloka/StudyHive/pull/33) | Checklist in PLAN.md section 5 and README (Production env, fresh Jwt__SigningKey / Agent__InternalApiKey, dev file never in the image, post-deploy dev-login must fail); image check: `/app` lists only `appsettings.json`, env `Production`; the Railway items are ticked by the human at deploy (Task 5) |
| CW-05 (API) | [#34](https://github.com/ItsAloka/StudyHive/pull/34) | `RolesAuditMaintenanceTests.The_Role_Table_Allows_And_Forbids_Reads` (13 cases), `…Only_A_Librarian_Writes_Maintenance_Windows`, `…Consumable_Put_Honours_IsActive_And_Keeps_It_When_Omitted`; live: Admin GET maintenance/suppliers/transactions 200 |
| CW-04 | [#34](https://github.com/ItsAloka/StudyHive/pull/34) | `RolesAuditMaintenanceTests.Room_Equipment_And_Maintenance_Writes_Are_Audited`, `…Store_Student_And_Booking_Writes_Are_Audited` (one row per write type); live: window create + delete → MaintenanceScheduled, MaintenanceCancelled with user |
| CW-06 (API) | [#34](https://github.com/ItsAloka/StudyHive/pull/34) | `RolesAuditMaintenanceTests.A_Window_Over_Confirmed_Bookings_Is_409_Listing_Them_Unless_Forced_Which_Emails_The_Students`, `…Future_Windows_Can_Be_Edited_And_Cancelled_But_Started_Ones_Cannot`; live: overlapping POST → 409 listing the booking |
| CW-07 (report, reservation context) | [#34](https://github.com/ItsAloka/StudyHive/pull/34) | `RolesAuditMaintenanceTests.The_Consumable_Report_Counts_Released_Reservations_Even_Without_A_Ledger_Row`, `…Reservations_Say_Who_And_When_And_Filter_By_The_Day_They_Are_Due`. Web columns follow in B3 |
| C-15 (hours, check-ins) | [#34](https://github.com/ItsAloka/StudyHive/pull/34) | `RoomCheckInAndUsageReportTests.Librarian_Can_Read_Room_Usage_For_A_Date_Range` (10:00 Colombo → hour 10; checkedIn 1) |
| CW-08 (API) | [#34](https://github.com/ItsAloka/StudyHive/pull/34) | `RolesAuditMaintenanceTests.The_Students_List_Shows_Name_Email_And_A_Suspended_Flag_And_Searches_By_Email`; live: search "suspended" → name, email, isSuspended=true |
| C-16 (API) | [#34](https://github.com/ItsAloka/StudyHive/pull/34) | `RolesAuditMaintenanceTests.Eligibility_Reports_How_Many_Requests_Were_Sent_This_Week`. Mobile display follows in B2 |
| C-06 (UI), C-14 | [#35](https://github.com/ItsAloka/StudyHive/pull/35) | mobile `booking_state_test`: checked-in detail shows room, Colombo slot, "Checked in at 14:05", Approved-by-librarian step, no actions; ended + Completed in Past; emulator: next booking "Thu 8 Oct · Quiet Study 101 · 10:00–11:00" Checked in, no check-in button |
| C-07 (UI) | [#35](https://github.com/ItsAloka/StudyHive/pull/35) | `booking_state_test`: Cancel asks first, Keep sends nothing; `canCancel` false once a slot started |
| C-02 (UI) | [#35](https://github.com/ItsAloka/StudyHive/pull/35) | `booking_state_test`: revision comment shown, Edit and resend → pre-filled form → PUT then submit, no new request |
| C-08 | [#35](https://github.com/ItsAloka/StudyHive/pull/35) | `booking_flow_test`: switching tabs reloads bookings; detail/tile pushes reload on return; app resume via WidgetsBindingObserver |
| C-09 | [#35](https://github.com/ItsAloka/StudyHive/pull/35) | `booking_state_test`: form starts on the chosen slot with Preferred room and sends it in notes; emulator: today's past slots disabled, Use 2:00 PM → pre-filled form for A-201 |
| C-11 | [#35](https://github.com/ItsAloka/StudyHive/pull/35) | `booking_state_test`: a failed send retries the same draft (one POST, then PUT + submit); Draft tile Send |
| C-13 | [#35](https://github.com/ItsAloka/StudyHive/pull/35) | bell → My bookings, search icon/Forgot/Change password/Notifications/Help removed (D4, D5); `reference_mobile_ui_test` updated; emulator profile shows the note |
| C-16 | [#35](https://github.com/ItsAloka/StudyHive/pull/35) | `booking_flow_test`: Home shows the eligibility endpoint's "2 of 3 used"; emulator: "3 of 3 used" (was "5 of 3") |
| CW-01 | [#36](https://github.com/ItsAloka/StudyHive/pull/36) | `B3Pages.test`: Add room server error renders inside the dialog; live: duplicate QR → message inside the dialog, nothing behind |
| CW-03 | [#36](https://github.com/ItsAloka/StudyHive/pull/36) | `B3Pages.test`: Sign out + user on Rooms (a former showUser={false} page); `showUser` prop removed from all 18 pages |
| CW-05 (web) | [#36](https://github.com/ItsAloka/StudyHive/pull/36) | `permissions.test` (allowed + forbidden per role, D2, no role); `App.test` Admin nav without Requests; `B3Pages.test` Add room hidden for StoreOfficer, maintenance form hidden for Admin; live: Admin Reservations/Suppliers/Maintenance load, no Requests link; consumable Deactivate |
| CW-06 (web) | [#36](https://github.com/ItsAloka/StudyHive/pull/36) | `B3Pages.test`: 409 overlap lists the student, confirm resends with force=true; live: Edit/Cancel on a planned window |
| CW-07 (web) | [#36](https://github.com/ItsAloka/StudyHive/pull/36) | `B3Pages.test`: student/request/room/booking columns, Due today sends dueOn; `StorePages.test` Issue/Release confirm; live: Issue dialog "2 × A4 printouts for Emu Tester" |
| CW-08 (web) | [#36](https://github.com/ItsAloka/StudyHive/pull/36) | live: Students shows name/email, "Suspended until 2026-10-29" for suspended@; admin panel edits penalties and suspended-until |
| CW-12 | [#36](https://github.com/ItsAloka/StudyHive/pull/36) | live: request detail "Quiet Study 101 · Thu 8 Oct · 10:00–11:00 · Checked in · 20:08"; usage report Checked in tile; calendar booker/purpose/Checked in columns (data from B3a schedule detail) |
| W-13 | [#36](https://github.com/ItsAloka/StudyHive/pull/36) | `B3Pages.test`: QR image + Print QR sticker; live: room detail draws the QR in the browser (qrcode) |
| W-25 / W-26 (D2) | [#36](https://github.com/ItsAloka/StudyHive/pull/36) | `permissions.test`: Users/Settings only outside production builds; live (production build): no Users/Settings links for Admin |
| C-17 | [#38](https://github.com/ItsAloka/StudyHive/pull/38) | `polish_test`: only rooms the availability search returns are Free now; failed search shows no tag; live: Free now on free rooms, Inactive on AUDIT Room Z |
| C-18 | [#38](https://github.com/ItsAloka/StudyHive/pull/38) | `polish_test`: greeting follows the Colombo clock (20:00 → Good evening); live: "Good afternoon" at 15:56 |
| C-19 | [#38](https://github.com/ItsAloka/StudyHive/pull/38) | live: room detail header under an app bar, clear of the status bar |
| C-20 | [#38](https://github.com/ItsAloka/StudyHive/pull/38) | code: `pushReplacement` both ways between Approval status and Cost breakdown |
| C-21 / D3 | [#38](https://github.com/ItsAloka/StudyHive/pull/38) | live: launcher icon and splash show the StudyHive mark; login logo asset; profile initials; no PHOTO/AVATAR placeholders |
| C-22 | [#38](https://github.com/ItsAloka/StudyHive/pull/38) | code: no SCAFFOLD/TODO left in `mobile/lib` |
| C-23 | 3.9 | release APK built and installed in the end-of-day check |
| CW-11 (mobile) | [#38](https://github.com/ItsAloka/StudyHive/pull/38) | code: approval timeline, booking history, check-in times via `utils/colombo_time.dart`; free-times grid uses the phone clock (correct on a Sri Lanka phone); quotation room lines via `formatItemName` in [#40](https://github.com/ItsAloka/StudyHive/pull/40) (`polish_test`) |
| CW-09 | [#39](https://github.com/ItsAloka/StudyHive/pull/39) | `App.test`: role-denied routes show "You don't have access" inside the shell; unknown URL → "Page not found" with Sign out; signed out → sign-in; live: store officer `/approvals`, `/no-such-page` |
| CW-10 | [#39](https://github.com/ItsAloka/StudyHive/pull/39) | `WebPolish.test`: item names, no stale S3 text, history timeline with room booking, "Request not found" + back link; live: "Whiteboard markers", real history |
| CW-11 (web) | [#39](https://github.com/ItsAloka/StudyHive/pull/39) | `WebPolish.test`: `formatItemName` → "Quiet Study 101 · Tue 13 Oct, 10:00"; request times without seconds; all date-times via `utils/colomboTime.ts`; live: quotation page has no ISO timestamp |
| CW-13 | [#39](https://github.com/ItsAloka/StudyHive/pull/39) | `ApprovalPages.test`: Reject confirms with the comment, Ask for a change can be backed out; `WebPolish.test`: Remove equipment confirms; Issue/Release in #36 |
| CW-14 | [#39](https://github.com/ItsAloka/StudyHive/pull/39) (form) | supplier form shows every problem and marks required fields; **cut**: supplier on Stock in and Suppliers tile, because the API has no consumable `suppliers[]` (B1 did not add it) |
| D3 (web login) | [#39](https://github.com/ItsAloka/StudyHive/pull/39) | live: login brand panel instead of the "library photograph" placeholder |
