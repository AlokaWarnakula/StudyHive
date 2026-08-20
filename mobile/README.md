# mobile/ — StudyHive Flutter app

State management: **Provider** (ADR-2). Talks only to `api/` — never directly to `agent/`.

## Status: verified

`flutter pub get`, `flutter analyze` (0 issues) and `flutter test` (5/5 passing) all run clean.
`flutter build web` also succeeds, so the app runs in Chrome without Android Studio or the Android
SDK. `web/` and `windows/` platform runners are committed; `android/`/`ios/` are not generated yet —
add them with `flutter create . --platforms=android,ios` once Android Studio/Xcode are available.

Login is wired to `POST /api/auth/login` (self-registration isn't in the app yet — use one of the
seeded dev accounts, or `POST /api/auth/register` directly, until S1 adds a sign-up screen). Only
Student accounts can sign in here; staff accounts are rejected with a message pointing at the React
dashboard. Tokens are stored via `flutter_secure_storage` (`lib/state/token_store.dart`) and the
app exchanges the stored refresh token for a fresh session on cold start.

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
- `lib/theme/` — colors lifted from `UI/StudyHive Mobile UI (offline).html`
- `lib/api/api_client.dart` — thin `http` wrapper, bearer auth, RFC7807 problem-body parsing
- `lib/state/auth_provider.dart` — session `ChangeNotifier`; `lib/state/token_store.dart` — the
  `flutter_secure_storage` wrapper it persists tokens through (abstracted so widget tests can swap
  in an in-memory fake instead of hitting a real platform channel)
- `lib/screens/` — Create / Track / Profile placeholders (owner: S1) behind a bottom nav shell
