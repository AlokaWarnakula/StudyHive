# Development accounts

Four accounts, one per role, for clicking through the app locally. **These already exist** — the API
seeds them into the database automatically on startup in Development. Nothing needs creating.

Every account verified by a real login against the running API on 2026-08-29: all four returned
HTTP 200 with the correct role and an access token.

> **Development only.** These credentials work solely against your local Docker PostgreSQL. They are
> already committed in `api/src/StudyHive.Api/appsettings.Development.json`, so this file is a
> convenience, not a new exposure. The API refuses to start outside Development without a real
> connection string, JWT signing key, agent key and explicit `AllowedHosts` — so these cannot be
> used against anything deployed. Never reuse this password anywhere real.

## The accounts

Password for all four: `Dev-Only-Passw0rd!`

| Role | Email | Signs in to | Sees |
|---|---|---|---|
| **Librarian** | `librarian@studyhive.dev` | Web dashboard | Booking requests, students, rooms, approvals, reports |
| **StoreOfficer** | `storeofficer@studyhive.dev` | Web dashboard | Consumables, low stock, reservations, suppliers |
| **Admin** | `admin@studyhive.dev` | Web dashboard | Everything, plus Users and Settings |
| **Student** | `student@studyhive.dev` | **Mobile app only** | Create and track their own requests |

### The student account will not log into the web dashboard — that is correct, not a bug

The staff console is staff-only by design (see `web/src/pages/auth/LoginPage.tsx`). Signing in with
`student@studyhive.dev` there gives you:

> This account can't sign in to the staff dashboard. Use the StudyHive mobile app instead.

That message is the feature working. Students use the Flutter app; the web app is for librarians,
store officers and admins. Use **`librarian@studyhive.dev`** for most of what you want to look at —
it owns the S1 screens that talk to the real API.

## The two URLs

| App | URL | Sign in as |
|---|---|---|
| **Web** — staff dashboard | <http://localhost:5173> | `librarian@studyhive.dev` (or storeofficer / admin) |
| **Mobile** — student app | <http://localhost:8090> | `student@studyhive.dev` |

Each app rejects the other's accounts on purpose. Staff cannot sign in to the mobile app, students
cannot sign in to the web dashboard, and both say so clearly rather than failing silently.

The mobile app is the Flutter build running in a browser. Narrow the window to a phone width, or use
your browser's device toolbar (F12, then the phone icon), or it stretches across the full page.

## Start everything

Four terminals from the repo root.

**1. Database**

```bash
docker compose up -d db
```

**2. API** — seeds the accounts on startup

```bash
dotnet run --project api/src/StudyHive.Api --no-launch-profile --urls http://localhost:5299
```

Confirm it is up:

```bash
curl http://localhost:5299/health
```

**3. Web app**

```bash
npm run dev --prefix web
```

Then open <http://localhost:5173> and sign in as the librarian.

**4. Mobile app** — build once, then serve it

```bash
flutter build web --dart-define=API_BASE_URL=http://localhost:5299
```

```bash
python -m http.server 8090 --directory mobile/build/web
```

Then open <http://localhost:8090> and sign in as the student.

The `--dart-define` matters. Without it the app defaults to `http://10.0.2.2:5299`, which is the
Android emulator's alias for the host machine and is unreachable from a desktop browser — sign-in
would fail with a network error that looks like the API is down when it is not.

`http://localhost:8090` is in `Cors:AllowedOrigins` in `appsettings.Development.json` alongside the
web app's `5173`. If you serve the mobile build on a different port, add that port there or the
browser blocks every API call.

## What you will actually see

The seeder creates real data to click through, not empty screens:

| | |
|---|---|
| Booking requests | 10, spread across 8 different statuses |
| Workflow executions | 8, with 28 step logs between them |
| Consumables | 3 |
| Student profiles | 2 |

The request statuses cover Draft, Processing, PendingApproval (×3), Approved, Rejected, Completed,
Cancelled and Failed — so the status timeline on the request detail screen has something to show in
each state, including the failure path.

### Which screens are real, and which are not

This matters, because most of the dashboard is deliberately not wired up yet:

- **Real, talking to the live API** — sign-in (W-01), Booking requests (W-10), Request detail
  (W-11), Students (W-12). These are S1's, and they are finished.
- **Development preview** — the S2/S3/S4 screens (rooms, consumables, approvals, reports) render
  the reference layout from a fixture set and label themselves as a preview. They are not lying
  about having data; the label is on the screen.
- **Not built yet** — anything whose endpoint does not exist returns a clear "not built yet" state
  rather than a blank page or fake numbers.

A production build strips the fixtures entirely, so those screens show the honest unavailable state
rather than demo data. That is checked by `web/tests/fixtures.prod.test.ts`.

## If sign-in fails

| Symptom | Cause |
|---|---|
| `Failed to fetch` / network error | The API is not running, or not on port 5299 |
| 401 on a correct password | The database was reset after the API started — restart the API so it reseeds |
| Student account rejected on web | Working as designed, see above |
| API will not start | Docker database is down: `docker compose up -d db` |

Reset the database completely and reseed:

```bash
docker compose down -v && docker compose up -d db
```

then restart the API — it recreates the schema and all four accounts. Seeding is idempotent, so
starting the API repeatedly does not duplicate anything.

## Related

- [`DATABASE.md`](DATABASE.md) — the full schema
- [`DOCS/database.md`](DOCS/database.md) — database setup and connection details
- [`DOCS/S1_Scope_And_Handoff.md`](DOCS/S1_Scope_And_Handoff.md) — what S1 owns and what is finished
