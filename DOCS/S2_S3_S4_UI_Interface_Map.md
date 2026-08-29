# S2/S3/S4 UI interface map

Every web and mobile screen for S2 (Rooms & Availability), S3 (Consumables & Stock) and S4
(Costing, Validation, Approval & Audit) already exists as a **presentation shell**: a real route,
real navigation, the reference layout, and an honest "not built yet" state. What none of them has is
data, because the endpoint behind it does not exist yet. That is your job.

This is the map from each shell to the file you edit and the endpoint you build.

> **Paths in this document were verified against the real tree on 2026-08-29.** An earlier version of
> this file pointed at `pages/librarian/…`, `pages/admin/…` and `web/src/types/*.ts`, which were
> removed when the web app was restructured to the 26-screen UI reference. If you are reading a copy
> that still mentions those, it is stale — the paths below are the current ones.

Each page file already carries its own screen ID, endpoint and owner in a docstring at the top. If
this document and the docstring ever disagree, **the docstring wins** — it lives next to the code.

## How to turn a shell on

**Web.** Every S2/S3/S4 page currently reads from the development fixture set:

```tsx
const fixture = useFixture((f) => f.rooms);
if (!fixture.enabled) return <Screen title="Rooms"><NotBuiltYet owner="S2 rooms" what="The room list" /></Screen>;
```

`useFixture` (`web/src/dev/useFixture.ts`) is compiled out of a production build entirely, so these
screens ship the honest unavailable state rather than demo data — `web/tests/fixtures.prod.test.ts`
checks that claim rather than trusting it. To make a screen real:

1. Add an API client beside the existing ones in `web/src/api/` — copy the shape of
   `web/src/api/bookingRequests.ts`, which is a real, working S1 client.
2. Replace the `useFixture` call with `useState` + `useEffect` against your client.
3. Keep the `NotBuiltYet` branch for the error/empty path, or swap it for a real empty state.

**`web/src/pages/requests/RequestsPage.tsx` is your reference implementation.** It is a genuine S1
screen and it already solves search, status filter, sort, pagination and the empty state against the
real API. Read it before writing your first list page.

There is no `web/src/types/` directory. Types live next to the client that returns them (see
`web/src/api/bookingRequests.ts`), and the fixture shapes are in `web/src/dev/fixtures.ts`.

**Mobile.** Same idea. Add a client in `mobile/lib/api/` copying `booking_requests_api.dart`, add a
`ChangeNotifier` provider copying `mobile/lib/state/booking_requests_provider.dart`, and register it
in `main.dart`'s `MultiProvider` sharing `authProvider.apiClient`. View models for the S2/S3/S4
screens already exist: `mobile/lib/models/room.dart`, `consumable.dart`, `quotation.dart`.

Files marked **(you create this)** below do not exist yet and are not supposed to.

## S2 — Rooms & Availability

| Screen | Web file | Mobile file | Endpoint to build |
|---|---|---|---|
| W-13 Rooms + add/edit dialog | `web/src/pages/rooms/RoomsPage.tsx` | `mobile/lib/screens/rooms/browse_rooms_screen.dart` (M-09) | `GET /api/rooms`, `POST/PUT /api/rooms` |
| W-14 Room detail & equipment | `web/src/pages/rooms/RoomDetailPage.tsx` | `mobile/lib/screens/rooms/room_detail_screen.dart` (M-10) | `GET /api/rooms/{id}` |
| W-15 Room calendar | `web/src/pages/rooms/RoomCalendarPage.tsx` | `mobile/lib/screens/rooms/room_schedule_screen.dart` (M-11) | `GET /api/rooms/schedule?from=&to=` |
| W-16 Equipment | `web/src/pages/rooms/EquipmentPage.tsx` | — staff only | `GET /api/equipment` |
| W-17 Maintenance windows | `web/src/pages/rooms/MaintenancePage.tsx` | — staff only | `POST /api/maintenance-windows` |
| W-18 Room utilisation report | `web/src/pages/reports/RoomUtilisationPage.tsx` | — staff only | `GET /api/reports/room-usage` |
| M-14 QR check-in | — no staff QR flow | `mobile/lib/screens/rooms/qr_check_in_screen.dart` | `POST /api/room-bookings/{id}/check-in` |
| M-15 Checked in | — | `mobile/lib/screens/rooms/checked_in_screen.dart` | success state of the above |

Web clients you create: `web/src/api/rooms.ts` **(you create this)**.
Mobile client you create: `mobile/lib/api/rooms_api.dart` **(you create this)**.

Web routes, already wired in `web/src/App.tsx`: `/rooms`, `/rooms/calendar`, `/rooms/:id`,
`/equipment`, `/maintenance`, `/reports/rooms`.

Your agent stage: replace the **Scheduling** stub in
`api/src/StudyHive.Api/Services/WorkflowOrchestrationService.cs` (`BuildSchedulingStub`). Its output
contract is `{ slots[{ roomId, roomName, startsAt, endsAt, hourlyRate }], conflicts[] }` — keep that
shape and the screens above keep working.

## S3 — Consumables & Stock

| Screen | Web file | Mobile file | Endpoint to build |
|---|---|---|---|
| W-19 Consumables | `web/src/pages/store/ConsumablesPage.tsx` | `mobile/lib/screens/consumables/browse_consumables_screen.dart` | `GET /api/consumables` |
| W-20 Consumable detail + stock-in | `web/src/pages/store/ConsumableDetailPage.tsx` | `mobile/lib/screens/consumables/consumable_detail_screen.dart` | `GET /api/consumables/{id}`, `POST /api/consumables/{id}/stock-in` |
| W-21 Low stock alerts | `web/src/pages/store/LowStockPage.tsx` | — staff only | `GET /api/consumables/low-stock` |
| W-22 Stock reservations | `web/src/pages/store/ReservationsPage.tsx` | — staff only | `GET /api/stock-reservations?status=` |
| W-23 Suppliers | `web/src/pages/store/SuppliersPage.tsx` | — staff only | `GET/POST/PUT /api/suppliers` |
| W-24 Consumable usage report | `web/src/pages/reports/ConsumableUsagePage.tsx` | — staff only | `GET /api/reports/consumable-usage` |
| Quantity picker | — | `mobile/lib/screens/consumables/select_consumables_screen.dart` | reads `GET /api/consumables` |

Web client you create: `web/src/api/consumables.ts` **(you create this)**.
Mobile client you create: `mobile/lib/api/consumables_api.dart` **(you create this)**.

Web routes: `/consumables`, `/consumables/low-stock`, `/consumables/:id`, `/reservations`,
`/suppliers`, `/reports/consumables`.

One thing to know: `select_consumables_screen.dart` is deliberately **not** wired into
`create_request_screen.dart`. S1's create form ships with no consumable selector on purpose, because
there is no real catalogue to select from yet. When your API exists, that screen becomes the
quantity-picker step the create form links out to — and `booking_request_items` already accepts the
result, validated by S1's `ValidateItemsOrProblemAsync`.

Your agent stage: replace the **Resource** stub (`BuildResourceStub`). Contract:
`{ items[{ consumableId, requested, available, sufficient, unitPrice, lineTotal }], totalCost, allAvailable }`.

## S4 — Costing, Validation, Approval & Audit

| Screen | Web file | Mobile file | Endpoint to build |
|---|---|---|---|
| W-03 Approval queue | `web/src/pages/approvals/ApprovalQueuePage.tsx` | — staff only | `GET /api/approvals?status=Pending` |
| W-04 Review proposal | `web/src/pages/approvals/ReviewProposalPage.tsx` | — staff only | `POST /api/approvals/{id}/decision` |
| W-05 Quotation detail | `web/src/pages/approvals/QuotationDetailPage.tsx` | `mobile/lib/screens/quotation/quotation_view_screen.dart` (M-08) | `GET /api/quotations/{id}` |
| W-06 Workflow execution viewer | `web/src/pages/approvals/WorkflowExecutionPage.tsx` | — staff only | `GET /api/workflow-executions/{id}` |
| W-07 Execution history | `web/src/pages/approvals/ExecutionHistoryPage.tsx` | — staff only | `GET /api/workflow-executions` |
| W-08 Audit log | `web/src/pages/approvals/AuditLogPage.tsx` | — staff only | `GET /api/audit-logs` |
| W-09 Reports hub | `web/src/pages/reports/ReportsPage.tsx` | — staff only | `GET /api/reports/bookings` |
| Approval status | — | `mobile/lib/screens/quotation/approval_status_screen.dart` | reads the request status |
| Booking history | — | `mobile/lib/screens/quotation/booking_history_screen.dart` | `GET /api/booking-requests?status=` |

Web client you create: `web/src/api/approvals.ts` **(you create this)**.

Web routes: `/approvals`, `/approvals/:id`, `/quotations/:id`, `/workflows`, `/workflows/:id`,
`/audit-log`, `/reports`.

Your agent stage: replace the **Validation** stub (`BuildValidationStub`). Contract:
`{ valid, results[{ rule, passed, detail }], quotation{...}, failures[] }`. The plan is specific
about what makes this an agent rather than a function: the agent chooses *which* checks apply to a
given proposal and turns failures into a readable revision instruction; the checks themselves are
arithmetic. Be ready to say that out loud in the viva.

W-04's approval is the one that matters most — the plan requires a **single database transaction**
that books the rooms and reserves the stock together, with the exclusion constraint and the stock
CHECK as the last line of defence inside it.

## Shared screens (not one owner's business component)

| Screen | Web file | Note |
|---|---|---|
| W-01 Staff sign in | `web/src/pages/auth/LoginPage.tsx` | Real and working. Foundation. |
| W-02 Dashboard | `web/src/pages/DashboardPage.tsx` | Its counts come from all four owners' read endpoints, so it goes live in pieces. |
| W-25 Users & roles | `web/src/pages/admin/UsersPage.tsx` | Admin. Needs `GET /api/users`. |
| W-26 Settings | `web/src/pages/admin/SettingsPage.tsx` | Admin. |

## S1's screens — already real, do not edit

These four web screens and the student mobile flows talk to the live API today. They are S1's
component. If you need a change in one, raise it with S1 rather than editing it — see
[`S1_Scope_And_Handoff.md`](S1_Scope_And_Handoff.md).

- `web/src/pages/requests/RequestsPage.tsx` (W-10)
- `web/src/pages/requests/RequestDetailPage.tsx` (W-11)
- `web/src/pages/requests/StudentsPage.tsx` (W-12)
- `web/src/pages/auth/LoginPage.tsx` (W-01)
- Mobile: `login_screen.dart`, `register_screen.dart`, `home_screen.dart`,
  `create_request_screen.dart`, `workflow_progress_screen.dart`, `track_screen.dart`,
  `booking_detail_screen.dart`, `profile_screen.dart`

## Rules that apply to every endpoint you build

From the plan's shared conventions — these are not suggestions, and S1 already configured all of
them, so you get them for free by not fighting them:

- **Lists** take `?page=1&pageSize=20&sortBy=&sortDir=&search=` and return
  `{ items, page, pageSize, totalItems, totalPages }`. Use `PagedResult<T>` and `PageQuery` from
  `api/src/StudyHive.Api/Common/PagedResult.cs`. Default `pageSize` 20, max 100. **An unknown
  `sortBy` is a 400, never a silent fallback.**
- **Errors** are RFC 7807 ProblemDetails from the one global handler. Never hand-roll an error body.
- **JSON** is camelCase with string enums; **Postgres** is snake_case. Both are configured in
  `Program.cs`. Do not add per-controller serializer settings.
- **Money** is `numeric(12,2)`. **Every instant** is `timestamptz`. **Every status column** is
  `varchar(30)` with a CHECK constraint and `.HasConversion<string>()` in C#.
- **Deletes are RESTRICT** by default. Rooms and consumables are deactivated, never deleted.

Your tables already exist in `StudyHiveDbContext` with configurations in
`api/src/StudyHive.Api/Data/Configurations/S2Configurations.cs`, `S3Configurations.cs` and
`S4Configurations.cs`. You should not need a new initial migration — add your own migration only for
changes you make to your own entities.
