# StudyHive Foundation Completion and S1 Readiness Plan

## Purpose

This document is the handoff checklist for completing the shared foundation before any student starts business-feature work. The implementation plan and database rules in `StudyHive_Master_Project_Relay_Plan.html` remain the source of truth.

The current repository is a runnable scaffold: API, web, mobile, agent, Docker PostgreSQL, CI and developer documentation are present. It is **not yet the Foundation Lock** described in the relay plan. Do not begin S1 feature work until the exit criteria below are met.

## Current verified state

- API, React web app, Flutter app and FastAPI agent all have runnable shells and automated checks.
- Docker Compose PostgreSQL is healthy; the API project can run and EF tooling is configured.
- CI runs the API, web, agent and mobile checks.
- `StudyHiveDbContext` currently contains no `DbSet` properties, and the existing `InitialCreate` migration has empty `Up` and `Down` methods. The database therefore contains no StudyHive application schema.
- JWT validation middleware and role policies are configured, but there is no working authentication system: no users or refresh-token tables, no password hashing, no login/refresh/logout endpoints, no test accounts and no real client sign-in integration.
- The API launch profile starts on port `5295`, while the README and clients use `5299`; this must be aligned.

## Foundation work to complete

### 1. Agree the shared data model

Before anyone creates a migration, all four students must review and agree the entity classes and relationships in the relay plan. Keep every entity in the single API `StudyHiveDbContext`.

At minimum, include skeletons for:

- Shared: `User`, `RefreshToken`.
- S1: `StudentProfile`, `BookingRequest`, `BookingRequestItem`, `WorkflowExecution`, `WorkflowStepLog`.
- S2: `StudyRoom`, `EquipmentType`, `RoomEquipment`, `BookingRequestEquipment`, `MaintenanceWindow`, `RoomBooking`.
- S3: `Consumable`, `Supplier`, `ConsumableSupplier`, `StockReservation`, `StockTransaction`, `EmailNotification`.
- S4: `Quotation`, `QuotationLineItem`, `ApprovalDecision`, `AuditLog`.

Apply the documented conventions: UUID keys, UTC timestamps, string-backed status enums with database check constraints, restrictive deletes by default, cascades only for owned child rows, and indexes/unique constraints described in the relay plan.

### 2. Create one real initial migration

After every skeleton is registered in `StudyHiveDbContext`:

1. Add EF Core configurations and relationships.
2. Generate one initial migration containing the complete agreed baseline schema.
3. Apply it to the local Docker PostgreSQL database.
4. Verify a clean database can be created from scratch using the documented commands.

Do not create separate initial migrations per student. Later migrations belong to the feature owner that changes their own component.

### 3. Implement shared authentication and authorization

This is shared infrastructure, not an individual student's business component.

- Create `users` and `refresh_tokens` persistence with a hashed password and hashed refresh-token storage.
- Implement register/login, refresh-token rotation/revocation and logout endpoints.
- Issue JWT access tokens with the documented role claims: Student, Librarian, StoreOfficer and Admin.
- Add role and ownership authorization helpers for future API controllers.
- Create development-only seed accounts using environment configuration; never commit production passwords or signing keys.
- Connect the React and Flutter login screens to the API. Web access tokens remain in memory; Flutter tokens use secure storage.
- Add API tests for login, invalid credentials, expired/revoked refresh tokens, role rejection and ownership rejection.

### 4. Correct foundation defects and hardening gaps

- Use one API port everywhere. Recommended: change the API launch profile to `5299`, which already matches the README, React and Flutter defaults.
- Protect every non-health FastAPI agent endpoint with the internal API key, using a timing-safe comparison. Deploy the agent with no public ingress.
- Fail API startup outside Development if the database connection string or JWT signing key is missing; do not fall back to known development values.
- Add the declared `infra/` directory with a short README describing local infrastructure, or remove it from the root layout documentation.
- Bind local Docker PostgreSQL to `127.0.0.1:5432` and keep its credentials clearly labelled development-only.
- Set explicit production host names instead of `AllowedHosts: "*"` before deployment.
- Add Flutter's `C:\Users\aloka\develop\flutter\bin` to the developer PATH so standard terminal commands work consistently.

### 5. Verify the completed Foundation Lock

Run and record these checks after the foundation changes:

```powershell
docker compose up -d db
dotnet ef database update --project api/src/StudyHive.Api --startup-project api/src/StudyHive.Api
dotnet test --project api/tests/StudyHive.Api.Tests
cd web; npm run lint; npm run test; npm run build
cd ../agent; .\.venv\Scripts\python.exe -m pytest -q
cd ../mobile; flutter pub get; flutter analyze; flutter test; flutter build web
```

Also verify:

- API health and Swagger respond on the documented port.
- A fresh database receives the full baseline schema.
- Login succeeds for each seeded development role and protected routes reject invalid/missing tokens.
- The agent health endpoint remains public only where intended; its configuration/workflow endpoints require the internal key.
- Web and mobile can sign in against the local API.

## Git and branch process

1. Review the completed foundation changes.
2. Make one initial commit on `main` only after all checks above are green.
3. Create one feature branch per student from that exact commit. Suggested names:
   - `s1/requests-workflow-planner`
   - `s2/rooms-availability-scheduling`
   - `s3/stock-resource-agent`
   - `s4/costing-approval-validation`
4. Each student edits only their owned component. Merge changes through a reviewed pull request; no direct feature work on `main`.

## Start S1 only when all exit criteria are met

S1 can begin when the following are true:

- All entity skeletons and the single initial migration are present and tested.
- Authentication and RBAC are functional from both clients.
- API/agent security and port/documentation issues above are resolved.
- The baseline has passed all four component checks and has a reviewed initial commit on `main`.
- S1's API contracts for requests/workflow and its S2–S4 stubs are written down.

Then S1 implements student profile, request draft/submit/track, eligibility, workflow status, planner-agent orchestration with contract-correct downstream stubs, React request views, Flutter create/track/QR flows, and tests. S2–S4 replace only their own stubs later without changing S1 contracts.
