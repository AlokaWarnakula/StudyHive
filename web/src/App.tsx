import { Route, Routes } from "react-router-dom";
import { AppShell } from "./components/AppShell";
import { NotFoundPage } from "./components/StatusPages";
import { ProtectedRoute } from "./routes/ProtectedRoute";
import { rolesFor } from "./auth/permissions";

import { LoginPage } from "./pages/auth/LoginPage";
import { DashboardPage } from "./pages/DashboardPage";

import { ApprovalQueuePage } from "./pages/approvals/ApprovalQueuePage";
import { ReviewProposalPage } from "./pages/approvals/ReviewProposalPage";
import { QuotationDetailPage } from "./pages/approvals/QuotationDetailPage";
import { WorkflowExecutionPage } from "./pages/approvals/WorkflowExecutionPage";
import { ExecutionHistoryPage } from "./pages/approvals/ExecutionHistoryPage";
import { AuditLogPage } from "./pages/approvals/AuditLogPage";

import { RequestsPage } from "./pages/requests/RequestsPage";
import { RequestDetailPage } from "./pages/requests/RequestDetailPage";
import { StudentsPage } from "./pages/requests/StudentsPage";

import { RoomsPage } from "./pages/rooms/RoomsPage";
import { RoomDetailPage } from "./pages/rooms/RoomDetailPage";
import { RoomCalendarPage } from "./pages/rooms/RoomCalendarPage";
import { EquipmentPage } from "./pages/rooms/EquipmentPage";
import { MaintenancePage } from "./pages/rooms/MaintenancePage";

import { ConsumablesPage } from "./pages/store/ConsumablesPage";
import { ConsumableDetailPage } from "./pages/store/ConsumableDetailPage";
import { LowStockPage } from "./pages/store/LowStockPage";
import { ReservationsPage } from "./pages/store/ReservationsPage";
import { SuppliersPage } from "./pages/store/SuppliersPage";

import { ReportsPage } from "./pages/reports/ReportsPage";
import { RoomUtilisationPage } from "./pages/reports/RoomUtilisationPage";
import { ConsumableUsagePage } from "./pages/reports/ConsumableUsagePage";

import { UsersPage } from "./pages/admin/UsersPage";
import { SettingsPage } from "./pages/admin/SettingsPage";

/**
 * The 26 screens of the reference document (W-01 … W-26), each on its own route.
 *
 * Every route's roles come from the one role table in auth/permissions.ts (AUDIT CW-05), which
 * mirrors the API's [Authorize] attributes; the nav (AppShell.tsx) and buttons ask the same table.
 */

export function App() {
  return (
    <Routes>
      {/* W-01 */}
      <Route path="/login" element={<LoginPage />} />

      <Route
        element={
          <ProtectedRoute>
            <AppShell />
          </ProtectedRoute>
        }
      >
        {/* W-02 — the one screen every staff role can reach. */}
        <Route path="/" element={<ProtectedRoute allow={rolesFor("dashboard.view")}><DashboardPage /></ProtectedRoute>} />

        {/* W-03 … W-08 — S4 */}
        <Route path="/approvals" element={<ProtectedRoute allow={rolesFor("approvals.view")}><ApprovalQueuePage /></ProtectedRoute>} />
        <Route path="/approvals/:id" element={<ProtectedRoute allow={rolesFor("approvals.view")}><ReviewProposalPage /></ProtectedRoute>} />
        <Route path="/quotations/:id" element={<ProtectedRoute allow={rolesFor("approvals.view")}><QuotationDetailPage /></ProtectedRoute>} />
        <Route path="/workflows" element={<ProtectedRoute allow={rolesFor("workflows.view")}><ExecutionHistoryPage /></ProtectedRoute>} />
        <Route path="/workflows/:id" element={<ProtectedRoute allow={rolesFor("workflows.view")}><WorkflowExecutionPage /></ProtectedRoute>} />
        <Route path="/audit-log" element={<ProtectedRoute allow={rolesFor("auditLog.view")}><AuditLogPage /></ProtectedRoute>} />

        {/* W-09, W-18, W-24 — the three reports share one tab strip */}
        <Route path="/reports" element={<ProtectedRoute allow={rolesFor("reports.view")}><ReportsPage /></ProtectedRoute>} />
        {/* The room-usage API is Librarian-only. */}
        <Route path="/reports/rooms" element={<ProtectedRoute allow={rolesFor("reports.rooms")}><RoomUtilisationPage /></ProtectedRoute>} />
        {/* W-24 is S3's report and the API serves it to StoreOfficer only. */}
        <Route path="/reports/consumables" element={<ProtectedRoute allow={rolesFor("reports.consumables")}><ConsumableUsagePage /></ProtectedRoute>} />

        {/* W-10 … W-12 — S1, the screens backed by real endpoints today */}
        <Route path="/requests" element={<ProtectedRoute allow={rolesFor("requests.view")}><RequestsPage /></ProtectedRoute>} />
        <Route path="/requests/:id" element={<ProtectedRoute allow={rolesFor("requests.view")}><RequestDetailPage /></ProtectedRoute>} />
        <Route path="/students" element={<ProtectedRoute allow={rolesFor("students.view")}><StudentsPage /></ProtectedRoute>} />

        {/* W-13 … W-17 — S2 */}
        <Route path="/rooms" element={<ProtectedRoute allow={rolesFor("rooms.view")}><RoomsPage /></ProtectedRoute>} />
        <Route path="/rooms/calendar" element={<ProtectedRoute allow={rolesFor("rooms.view")}><RoomCalendarPage /></ProtectedRoute>} />
        <Route path="/rooms/:id" element={<ProtectedRoute allow={rolesFor("rooms.view")}><RoomDetailPage /></ProtectedRoute>} />
        <Route path="/equipment" element={<ProtectedRoute allow={rolesFor("equipment.view")}><EquipmentPage /></ProtectedRoute>} />
        <Route path="/maintenance" element={<ProtectedRoute allow={rolesFor("maintenance.view")}><MaintenancePage /></ProtectedRoute>} />

        {/* W-19 … W-23 — S3 */}
        <Route path="/consumables" element={<ProtectedRoute allow={rolesFor("consumables.view")}><ConsumablesPage /></ProtectedRoute>} />
        <Route path="/consumables/low-stock" element={<ProtectedRoute allow={rolesFor("lowStock.view")}><LowStockPage /></ProtectedRoute>} />
        <Route path="/consumables/:id" element={<ProtectedRoute allow={rolesFor("consumables.view")}><ConsumableDetailPage /></ProtectedRoute>} />
        <Route path="/reservations" element={<ProtectedRoute allow={rolesFor("reservations.view")}><ReservationsPage /></ProtectedRoute>} />
        <Route path="/suppliers" element={<ProtectedRoute allow={rolesFor("suppliers.view")}><SuppliersPage /></ProtectedRoute>} />

        {/* W-25, W-26 — admin only */}
        <Route path="/users" element={<ProtectedRoute allow={rolesFor("users.view")}><UsersPage /></ProtectedRoute>} />
        <Route path="/settings" element={<ProtectedRoute allow={rolesFor("settings.view")}><SettingsPage /></ProtectedRoute>} />

        {/* CW-09: an unknown URL while signed in stays in the shell; signed out, the shell's guard
            sends it to /login?next=… like any other page. */}
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}

export default App;
