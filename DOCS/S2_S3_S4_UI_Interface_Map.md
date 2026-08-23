# S2/S3/S4 UI interface map

Every web and mobile screen for S2 (Rooms & Availability), S3 (Consumables & Stock), and S4
(Costing, Validation, Approval & Audit) now exists as a **presentation shell** — real routes, real
navigation, real typed view models, real loading/empty/unavailable/error states — with no live data
and no calls to an endpoint that doesn't exist yet. This is the map from each shell to what its owner
needs to build to make it real: the API endpoint, and where the typed view model already lives.

No frontend code changes are needed to "turn a screen on" beyond: build the endpoint, replace the
shell's hardcoded `status="unavailable"` with a real fetch, and pass real data as `children`/rows.
The shells (`ListShell`, `DetailShell`, `FormShell`, `DashboardShell` on web;
`ListShell`/`DetailShell` on mobile) already handle every other state.

## How to "turn on" a shell (both clients, same shape)

**Web** (e.g. `web/src/pages/librarian/rooms/RoomsListPage.tsx`): add a `useEffect` that calls a new
`web/src/api/rooms.ts` client (same pattern as `web/src/api/bookingRequests.ts`), track
`status`/`data` in `useState`, and pass `status="ready"` with real `<tbody>` rows once loaded — see
`RequestsPage.tsx` for the exact reference pattern (search/filter/sort/pagination all already solved
there).

**Mobile** (e.g. `mobile/lib/screens/rooms/browse_rooms_screen.dart`): replace the typed Debug-only
preview source with a
`mobile/lib/api/rooms_api.dart` client (same pattern as `booking_requests_api.dart`), a
`RoomsProvider extends ChangeNotifier` (same pattern as `booking_requests_provider.dart`, registered
in `main.dart`'s `MultiProvider` sharing `authProvider.apiClient`), then map its real state into the
existing reference-aligned screens.

## S2 — Rooms & Availability

View models: `web/src/types/rooms.ts` (web) / `mobile/lib/models/room.dart` (mobile).

| Screen (mockup name) | Web file | Mobile file | View model | Suggested endpoint |
|---|---|---|---|---|
| Rooms list | `pages/librarian/rooms/RoomsListPage.tsx` | `screens/rooms/browse_rooms_screen.dart` | `RoomListItem` | `GET /api/rooms` (paginated, per DOCS §12 list convention) |
| Room detail + equipment | `pages/librarian/rooms/RoomDetailPage.tsx` | `screens/rooms/room_detail_screen.dart` | `RoomDetail` | `GET /api/rooms/{id}` |
| Add/Edit room form | `pages/librarian/rooms/RoomFormPage.tsx` | — (staff-only) | — | `POST /api/rooms`, `PUT /api/rooms/{id}` |
| Room schedule calendar | `pages/librarian/rooms/RoomScheduleCalendarPage.tsx` | `screens/rooms/room_schedule_screen.dart` | `RoomScheduleSlot` | `GET /api/rooms/{id}/schedule?from=&to=` |
| Equipment management | `pages/librarian/rooms/EquipmentManagementPage.tsx` | — (staff-only) | `EquipmentTypeItem` | `GET/POST /api/equipment-types` |
| Maintenance windows | `pages/librarian/rooms/MaintenanceWindowsPage.tsx` | — (staff-only) | `MaintenanceWindowItem` | `GET/POST /api/maintenance-windows` |
| Room usage dashboard | `pages/librarian/rooms/RoomUsageDashboardPage.tsx` | — (staff-only) | `RoomUsageStat` | `GET /api/rooms/usage-report` |
| QR check-in | — (staff has no QR flow) | `screens/rooms/qr_check_in_screen.dart` | — | Needs a scan endpoint, e.g. `POST /api/rooms/check-in { qrCode }` |

Web routes: `/rooms`, `/rooms/:id`, `/rooms/new`, `/rooms/schedule`, `/rooms/equipment`,
`/rooms/maintenance`, `/rooms/usage` (all under the `AreaLayout` tab strip in `App.tsx`).

## S3 — Consumables & Stock

View models: `web/src/types/consumables.ts` (web) / `mobile/lib/models/consumable.dart` (mobile).

| Screen (mockup name) | Web file | Mobile file | View model | Suggested endpoint |
|---|---|---|---|---|
| Consumables list | `pages/store/ConsumablesListPage.tsx` | `screens/consumables/browse_consumables_screen.dart` | `ConsumableListItem` | `GET /api/consumables` |
| Consumable detail + history | `pages/store/ConsumableDetailPage.tsx` | `screens/consumables/consumable_detail_screen.dart` | `ConsumableDetail`, `StockTransactionItem` | `GET /api/consumables/{id}`, `GET /api/consumables/{id}/transactions` |
| Add/Edit consumable form | `pages/store/ConsumableFormPage.tsx` | — (staff-only) | — | `POST /api/consumables`, `PUT /api/consumables/{id}` |
| Stock-in form | `pages/store/StockInFormPage.tsx` | — (staff-only) | — | `POST /api/consumables/{id}/stock-in` |
| Low-stock alerts | `pages/store/LowStockAlertsPage.tsx` | — (staff-only) | `ConsumableListItem[]` | `GET /api/consumables?lowStock=true` |
| Stock reservations | `pages/store/StockReservationsPage.tsx` | — (staff-only) | `StockReservationItem` | `GET /api/stock-reservations?status=` |
| Suppliers | `pages/store/SuppliersPage.tsx` | — (staff-only) | `SupplierItem` | `GET/POST /api/suppliers` |
| Select consumables for booking | — (S1 owns booking requests; see note below) | `screens/consumables/select_consumables_screen.dart` | — | `GET /api/consumables` (reuse the list endpoint); replace the Debug-only picker records, which already round-trip through `BookingRequestsApi.create` |

Web routes: `/stock`, `/stock/:id`, `/stock/new`, `/stock/stock-in`, `/stock/low-stock`,
`/stock/reservations`, `/stock/suppliers`.

**Note for S3:** S1's `CreateBookingRequestRequest.items` (`api/src/StudyHive.Api/Controllers/BookingRequests/BookingRequestContracts.cs`)
and the equivalent Flutter `BookingRequestsApi.create` already accept a `List<{consumableId, quantity}>`
— that contract is stable and won't need to change. The quantity picker is already wired into the
three-step create flow; S3 replaces its Debug-only catalog with the real consumables list API.

## S4 — Costing, Validation, Approval & Audit

View models: `web/src/types/approvals.ts` (web) / `mobile/lib/models/quotation.dart` (mobile).
S4 also reuses S1's already-implemented `WorkflowExecution`/`WorkflowStepLog` shape — see
`web/src/api/bookingRequests.ts`'s `WorkflowStatusResponse` — rather than inventing a parallel one.

| Screen (mockup name) | Web file | Mobile file | View model | Suggested endpoint |
|---|---|---|---|---|
| Approval queue | `pages/admin/ApprovalQueuePage.tsx` | — (staff-only) | `ApprovalQueueItem` | `GET /api/quotations?status=Proposed` |
| Approval form | `pages/admin/ApprovalFormPage.tsx` | `screens/quotation/approval_status_screen.dart` (read-only view for the student) | `ApprovalDecisionInput` | `POST /api/quotations/{id}/approval-decisions` |
| Quotation detail | `pages/admin/QuotationDetailPage.tsx` | `screens/quotation/quotation_view_screen.dart` | `QuotationDetailView` | `GET /api/quotations/{id}` |
| Workflow execution viewer | `pages/admin/WorkflowExecutionViewerPage.tsx` | — (S1's per-request timeline already covers the student view) | reuses `WorkflowStatusResponse` | `GET /api/workflow-executions/{id}` (standalone lookup; S1's `GET /api/booking-requests/{id}/status` already covers the per-request case) |
| Execution history | `pages/admin/ExecutionHistoryPage.tsx` | — (staff-only) | `ExecutionHistoryItem` | `GET /api/workflow-executions?status=` |
| Audit log viewer | `pages/admin/AuditLogViewerPage.tsx` | — (staff-only) | `AuditLogEntryView` | `GET /api/audit-log?action=&entityType=&userId=` |
| Reports dashboard | `pages/admin/ReportsDashboardPage.tsx` | — (staff-only) | `ReportsSummary` | `GET /api/reports/summary` |
| Booking history with costs | — (staff sees it via Execution history / Reports) | `screens/quotation/booking_history_screen.dart` | `BookingHistoryItem` | `GET /api/booking-requests?studentOwn=true&status=Completed` plus quotation totals |

Web routes: `/admin`, `/admin/approvals/:id`, `/admin/quotations/:id`, `/admin/workflows/:id`,
`/admin/executions`, `/admin/audit-log`, `/admin/reports`.

## Mobile navigation note

The reference-aligned four-tab bottom nav is Home/Rooms/Bookings/Profile. Create Request begins from
Home; Rooms opens its browse/detail/schedule chain; Bookings opens request detail, quotation/history
and approval status; Profile holds the remaining secondary links. QR check-in is reached from an
eligible booking, not from a top-level tab.
