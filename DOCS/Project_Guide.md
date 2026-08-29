# StudyHive — plain-language project guide

This is a plain-English walkthrough of the project: what it is, how the team is splitting the work,
what's already built, and what's left. If you want the full formal spec, that's
[`StudyHive_Master_Project_Relay_Plan.html`](StudyHive_Master_Project_Relay_Plan.html) — this doc is
the "explain it to me like I've been away" version.

## What StudyHive actually is

A university system for booking study rooms and the supplies that go with them (markers, cables,
printouts, etc). A student describes what they need in plain English ("group study room for 4, this
Thursday afternoon, need a whiteboard"), the system runs that through an AI workflow that plans out
the request, a librarian reviews and approves it, and the student gets a confirmed booking.

Four parts, three ways to use it:

- **Flutter mobile app** — what students use to create and track requests.
- **React web dashboard** — what librarians / store officers / admins use to review and manage.
- **ASP.NET Core API** — the only thing either client talks to. It owns the database.
- **FastAPI "agent" service** — internal only, never reachable from the apps directly. The API calls
  it to run the AI planning step. Nobody outside the API can reach it.

## How the team is splitting the work: the "relay"

Instead of everyone touching everything at once, the project is split into **four business areas**,
each owned by one person, built **one at a time, in order**, each handing off a fully working system
to the next:

| Who | Owns | Status |
|---|---|---|
| Shared foundation | Login, navigation shells, database connection, CI | ✅ Done |
| **S1** | **Requests & Workflow + the Planner AI agent** | ✅ Done (this is what was just built) |
| S2 | Rooms & Availability + the Scheduling AI agent | ⏳ Next |
| S3 | Consumables & Stock (markers, cables, etc.) + the Resource AI agent | ⏳ After S2 |
| S4 | Costing, Approval workflow, Audit log + the Validation AI agent | ⏳ Last |

The rule: **whoever's turn it is builds their whole slice** — database tables, API, the React screens,
the Flutter screens, tests, and their one AI agent — before handing the repo to the next person. While
waiting for your turn, you're not idle: you review PRs, write tests, prep your own screens' contracts,
so everyone has visible contribution the whole time, but only one person's *business logic* changes
at once. See DOCS §03/04 in the master plan for the exact rubric reasoning behind this.

## What "S1" means, concretely, and what just got built

S1 owns the entire lifecycle of a booking request:

1. A student registers with their department and year, so their **student profile** is created with
   the account. The separate profile endpoint remains available for older clients.
2. They fill in a **booking request**: what it's for, group size, preferred dates/times, budget.
3. They hit **submit**. This is where the AI comes in: the system checks they're eligible (active
   account, not suspended, fewer than 3 penalty points, haven't hit their weekly booking limit), then hands the
   request to the **Planner Agent** — a small AI service that builds a step-by-step plan for who needs
   to do what next (find a room, check supplies, calculate the final cost).
4. The request lands in front of a librarian as **Pending Approval**, with the full plan visible.

That whole loop — profile, request, eligibility check, AI planning, and a librarian being able to see
it happen step by step — is what's now built and working, end to end, on both the phone app and the
web dashboard.

In Development, the seeded student starts with eight requests that demonstrate Draft, Processing,
Pending Approval, Approved, Rejected, Cancelled, Completed and Failed screens. These are real S1
database rows with workflow executions and step logs, not client-only mock requests. Screens owned
by later S2-S4 slices use clearly isolated Debug/Development preview adapters until their APIs exist.

**One deliberate limit:** the AI planning step currently produces a *placeholder* answer for "which
room" and "which supplies", clearly marked as a stub. That's intentional — rooms belong to S2 and
supplies belong to S3, who haven't been built yet. S1 wires the whole pipeline together and proves it
works; S2 and S3 will swap the placeholder answers for the real ones without S1 having to change
anything else.

## What's left (S2 → S3 → S4)

- **S2 (Rooms & Availability):** the actual list of rooms, what's in them, when they're free, and a
  real "find me an available room" search that replaces S1's placeholder room answer.
- **S3 (Consumables & Stock):** the actual inventory of markers/cables/printouts, suppliers, and stock
  levels that replaces S1's placeholder supply answer — with a guarantee the system never promises
  more stock than actually exists.
- **S4 (Costing, Approval & Audit):** the real price calculation, the librarian's approve/reject/
  request-changes buttons actually doing something, and a permanent audit trail of every decision.

Each of those follows the exact same pattern S1 just proved out: database + API + web screens + phone
screens + tests + one AI agent, handed off as a complete working system.

## Running it locally (quick version)

Full details are in the root [`README.md`](../README.md); short version:

```bash
docker compose up -d db                 # 1. database
cd api && dotnet run --project src/StudyHive.Api      # 2. API — http://localhost:5299
cd agent && ./.venv/Scripts/python -m uvicorn app.main:app --port 8001   # 3. AI agent service
cd web && npm run dev                   # 4. staff dashboard — http://localhost:5173
cd mobile && flutter run -d chrome --dart-define=API_BASE_URL=http://localhost:5299   # 5. student app
```

Test logins (local only, password `Dev-Only-Passw0rd!` for all of them):

| Email | Use it in |
|---|---|
| `student@studyhive.dev` | Flutter mobile app |
| `librarian@studyhive.dev` | React web dashboard |
| `storeofficer@studyhive.dev` | React web dashboard |
| `admin@studyhive.dev` | React web dashboard |

## Where to look for more

- **The full spec:** [`StudyHive_Master_Project_Relay_Plan.html`](StudyHive_Master_Project_Relay_Plan.html)
  — every database table, every API endpoint, every AI agent's exact contract.
- **What's implemented right now:** the "S1 — Requests & Workflow + Planner: implemented" section near
  the bottom of the root [`README.md`](../README.md) lists every endpoint, screen, and test file.
- **The database design:** [`database.md`](database.md).
