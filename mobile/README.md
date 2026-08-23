# mobile/ — StudyHive Flutter app

State management: **Provider** (ADR-2). Talks only to `api/` — never directly to `agent/`.

## Status: verified

`flutter pub get`, `flutter analyze` (0 issues) and `flutter test` (40/40 passing) all run clean.
`flutter build web` also succeeds, so the app runs in Chrome without Android Studio or the Android
SDK. `web/` and `windows/` platform runners are committed; `android/`/`ios/` are not generated yet —
add them with `flutter create . --platforms=android,ios` once Android Studio/Xcode are available.

Login is wired to `POST /api/auth/login`; the create-account screen uses `POST /api/auth/register`
and signs the new student in. Only Student accounts can sign in here; staff accounts are rejected with a message pointing
at the React dashboard. Tokens are stored via `flutter_secure_storage` (`lib/state/token_store.dart`)
and the app exchanges the stored refresh token for a fresh session on cold start.

S1's Requests & Workflow feature is implemented end to end:

- **Profile** — a student without a profile sees a one-time onboarding form
  (`POST /api/student-profiles`); once it exists the tab shows the read-only profile view.
- **Book a room** — the reference-aligned three-step request flow: purpose/date/budget, optional
  consumables, then review. It creates a Draft and immediately submits it
  (`POST /api/booking-requests` → `POST .../submit`) to trigger the Planner Agent workflow.
  The item picker uses deterministic Development-only preview records until S3 exposes its API.
- **Bookings** — "My Bookings", split into Active/Waiting/Past, backed by `GET /api/booking-requests`.
- **Booking detail** (reached from Bookings) — the status/workflow timeline, polling
  `GET .../{id}/status` every 3s while the workflow is still running, with a Cancel action for any
  non-terminal request.

All 16 reference frames are represented. S1 screens use the real API. S2-S4 rooms, quotation,
history and QR screens use typed preview records only when `ENABLE_DEMO_DATA=true`; it defaults to
on in debug builds and off in release builds, so preview data cannot appear as production state.
The real QR camera and check-in endpoint remain owned by S2.

## Design system

`UI/StudyHive Mobile UI (offline).html` is the source of truth for how the app looks, exactly as
`UI/StudyHive Web UI (offline).html` is for `web/`. Both clients carry the same tokens:

- `lib/theme/app_theme.dart` holds the reference's `:root` block — the accent and neutral ramps, the
  3.4px spacing base, the radius scale (tiles are square; only controls take the 4px radius) and the
  Barlow / Barlow Condensed pairing.
- `lib/widgets/studyhive_ui.dart` is the Flutter mirror of `web/src/components/ui.tsx`: one widget
  per reference class — `Tile` (`.tile`), `Kv` (`.kv`), `Lbl` (`.lbl`), `Big` (`.big`), `FNote`
  (`.fnote`), `ShTag` (`.tag-accent` / `.tag-outline` / `.tag-neutral`), `Segmented` (`.seg`),
  `StepperBar` (`.stepper`), `Timeline` (`.tl`), `Ph` (`.ph`), `BottomNav` (`.mnav`) and the
  `btn-lg` button trio.

Two consequences worth knowing before editing a screen:

- **Status has three tones, not five.** The reference palette contains no red, green or amber;
  a status reads as accent (affirmative), outline (in flight) or neutral (settled). `ShTag.forStatus`
  owns that mapping. Errors are shown in the accent-bordered `InlineError` panel, never a popup.
- **Field labels sit above their box** (`.field > label`), so the label is a sibling of the input
  rather than a child. Each `ShTextField` keys its control `field:<label>`; tests target one with the
  `field(...)` helper in `test/support/finders.dart`.

`test/reference_mobile_ui_test.dart` checks each frame against what the reference draws, and
`test/frame_layout_test.dart` pumps every frame at the reference's own 390 x 800 size and fails on
any overflow.

## Setup

1. Install the Flutter SDK (stable channel) from https://docs.flutter.dev/get-started/install/windows
   and add `<flutter-sdk>\bin` to your PATH (a new terminal is required afterward).
2. Run `flutter doctor`. `Chrome` and `Windows (desktop)` are enough to develop against — Android
   Studio/the Android SDK are only needed later for an emulator or a real APK build.
3. `cd mobile && flutter pub get`

## Running against the local API

Fastest path — no Android tooling required:

```bash
flutter run -d chrome --dart-define=API_BASE_URL=http://localhost:5299
```

The API base URL defaults to `http://10.0.2.2:5299` (the Android emulator's alias for the host
machine's `localhost`) when no override is given. Override it for Chrome/Windows desktop, a
physical device, or a different port:

```bash
flutter run --dart-define=API_BASE_URL=http://<your-lan-ip>:5299
```

## Structure

- `lib/app.dart` — root `MaterialApp`, switches between `LoginScreen` and `HomeScreen` on auth state
- `lib/theme/` — design tokens lifted from `UI/StudyHive Mobile UI (offline).html` (see Design system above)
- `lib/widgets/studyhive_ui.dart` — the reference component kit every screen is assembled from
- `lib/api/api_client.dart` — thin `http` wrapper, bearer auth, RFC7807 problem-body parsing;
  `lib/api/booking_requests_api.dart` and `lib/api/student_profiles_api.dart` — typed calls built on it
- `lib/models/` — plain data classes for the API's JSON responses (`BookingRequest`, `WorkflowStatus`, `StudentProfile`)
- `lib/state/auth_provider.dart` — session `ChangeNotifier`; `lib/state/token_store.dart` — the
  `flutter_secure_storage` wrapper it persists tokens through (abstracted so widget tests can swap
  in an in-memory fake instead of hitting a real platform channel). `lib/state/profile_provider.dart`
  and `lib/state/booking_requests_provider.dart` share `AuthProvider`'s single `ApiClient` instance
  (see `main.dart`) so every provider always sends whichever access token is currently active.
- `lib/screens/` — Home / Rooms / Bookings / Profile behind the four-tab shell, plus registration,
  the three-step booking flow, workflow progress, booking detail, quotation and QR/check-in screens
- `lib/data/demo_seed.dart` — deterministic debug-only S2-S4 preview records; disabled by default in release
