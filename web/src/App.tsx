import { Navigate, Route, Routes } from "react-router-dom";
import { AppShell } from "./components/AppShell";
import { ProtectedRoute } from "./routes/ProtectedRoute";
import type { StaffRole } from "./store/authStore";

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
 * Per-area role scopes follow the DOCS §11 API tables. A role outside an area's list never reaches
 * that area's route, and never sees its nav link either (see AppShell.tsx).
 */
const S1_ROLES: StaffRole[] = ["Librarian", "Admin"]; // Requests & students
const S2_ROLES: StaffRole[] = ["Librarian", "Admin"]; // Rooms, equipment, maintenance
const S3_ROLES: StaffRole[] = ["StoreOfficer", "Admin"]; // Consumables & stock
const S4_ROLES: StaffRole[] = ["Librarian", "Admin"]; // Approvals, costing, workflow, audit
const ADMIN_ROLES: StaffRole[] = ["Admin"]; // Users & settings
const ALL_STAFF: StaffRole[] = ["Librarian", "StoreOfficer", "Admin"];

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
        <Route path="/" element={<ProtectedRoute allow={ALL_STAFF}><DashboardPage /></ProtectedRoute>} />

        {/* W-03 … W-08 — S4 */}
        <Route path="/approvals" element={<ProtectedRoute allow={S4_ROLES}><ApprovalQueuePage /></ProtectedRoute>} />
        <Route path="/approvals/:id" element={<ProtectedRoute allow={S4_ROLES}><ReviewProposalPage /></ProtectedRoute>} />
        <Route path="/quotations/:id" element={<ProtectedRoute allow={S4_ROLES}><QuotationDetailPage /></ProtectedRoute>} />
        <Route path="/workflows" element={<ProtectedRoute allow={S4_ROLES}><ExecutionHistoryPage /></ProtectedRoute>} />
        <Route path="/workflows/:id" element={<ProtectedRoute allow={S4_ROLES}><WorkflowExecutionPage /></ProtectedRoute>} />
        <Route path="/audit-log" element={<ProtectedRoute allow={S4_ROLES}><AuditLogPage /></ProtectedRoute>} />

        {/* W-09, W-18, W-24 — the three reports share one tab strip */}
        <Route path="/reports" element={<ProtectedRoute allow={S4_ROLES}><ReportsPage /></ProtectedRoute>} />
        <Route path="/reports/rooms" element={<ProtectedRoute allow={S2_ROLES}><RoomUtilisationPage /></ProtectedRoute>} />
        <Route path="/reports/consumables" element={<ProtectedRoute allow={S4_ROLES}><ConsumableUsagePage /></ProtectedRoute>} />

        {/* W-10 … W-12 — S1, the screens backed by real endpoints today */}
        <Route path="/requests" element={<ProtectedRoute allow={S1_ROLES}><RequestsPage /></ProtectedRoute>} />
        <Route path="/requests/:id" element={<ProtectedRoute allow={S1_ROLES}><RequestDetailPage /></ProtectedRoute>} />
        <Route path="/students" element={<ProtectedRoute allow={S1_ROLES}><StudentsPage /></ProtectedRoute>} />

        {/* W-13 … W-17 — S2 */}
        <Route path="/rooms" element={<ProtectedRoute allow={S2_ROLES}><RoomsPage /></ProtectedRoute>} />
        <Route path="/rooms/calendar" element={<ProtectedRoute allow={S2_ROLES}><RoomCalendarPage /></ProtectedRoute>} />
        <Route path="/rooms/:id" element={<ProtectedRoute allow={S2_ROLES}><RoomDetailPage /></ProtectedRoute>} />
        <Route path="/equipment" element={<ProtectedRoute allow={S2_ROLES}><EquipmentPage /></ProtectedRoute>} />
        <Route path="/maintenance" element={<ProtectedRoute allow={S2_ROLES}><MaintenancePage /></ProtectedRoute>} />

        {/* W-19 … W-23 — S3 */}
        <Route path="/consumables" element={<ProtectedRoute allow={S3_ROLES}><ConsumablesPage /></ProtectedRoute>} />
        <Route path="/consumables/low-stock" element={<ProtectedRoute allow={S3_ROLES}><LowStockPage /></ProtectedRoute>} />
        <Route path="/consumables/:id" element={<ProtectedRoute allow={S3_ROLES}><ConsumableDetailPage /></ProtectedRoute>} />
        <Route path="/reservations" element={<ProtectedRoute allow={S3_ROLES}><ReservationsPage /></ProtectedRoute>} />
        <Route path="/suppliers" element={<ProtectedRoute allow={S3_ROLES}><SuppliersPage /></ProtectedRoute>} />

        {/* W-25, W-26 — admin only */}
        <Route path="/users" element={<ProtectedRoute allow={ADMIN_ROLES}><UsersPage /></ProtectedRoute>} />
        <Route path="/settings" element={<ProtectedRoute allow={ADMIN_ROLES}><SettingsPage /></ProtectedRoute>} />
      </Route>

      <Route path="*" element={<Navigate to="/login" replace />} />
    </Routes>
  );
}

export default App;
