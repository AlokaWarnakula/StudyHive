import type { StaffRole } from "../store/authStore";

/**
 * AUDIT CW-05: the one role table. Routes (App.tsx), nav links (AppShell.tsx) and action buttons all
 * ask `can(role, action)`, and every row mirrors the API's own [Authorize] attribute for the call
 * that action makes (after PLAN.md B1), so a role never sees a link or button the API would refuse.
 */
export type Action =
  | "dashboard.view"
  // S4
  | "approvals.view"
  | "workflows.view"
  | "auditLog.view"
  // S1
  | "requests.view" // GET /api/booking-requests: Librarian only
  | "payments.record" // POST /api/booking-requests/{id}/payment: Librarian
  | "students.view" // GET /api/student-profiles: Librarian, Admin
  | "students.edit" // PUT /api/student-profiles/{id}: Admin
  // S2
  | "rooms.view"
  | "rooms.create" // POST /api/rooms: Librarian, Admin
  | "rooms.edit" // PUT /api/rooms/{id}: Librarian, Admin
  | "rooms.deactivate" // DELETE /api/rooms/{id}: Admin
  | "rooms.equipment" // POST/DELETE /api/rooms/{id}/equipment: Librarian
  | "equipment.view"
  | "equipment.create" // POST /api/equipment: Librarian, Admin
  | "equipment.edit" // PUT /api/equipment/{id}: Librarian
  | "maintenance.view" // GET /api/maintenance-windows: Librarian, Admin
  | "maintenance.edit" // POST/PUT/DELETE /api/maintenance-windows: Librarian
  // S3
  | "consumables.view"
  | "consumables.create" // POST /api/consumables: StoreOfficer, Admin
  | "consumables.edit" // PUT /api/consumables/{id}: StoreOfficer
  | "consumables.deactivate" // StoreOfficer: PUT isActive=false; Admin: DELETE
  | "consumables.stockIn" // POST /api/consumables/{id}/stock-in: StoreOfficer
  | "consumables.ledger" // GET /api/stock-transactions: StoreOfficer, Admin
  | "lowStock.view" // GET /api/consumables/low-stock: StoreOfficer (Librarian has no store pages)
  | "reservations.view" // GET /api/stock-reservations: StoreOfficer, Librarian, Admin
  | "reservations.act" // PUT …/use, …/release: StoreOfficer
  | "suppliers.view" // GET /api/suppliers: StoreOfficer, Admin
  | "suppliers.create" // POST /api/suppliers: StoreOfficer, Admin
  | "suppliers.edit" // PUT /api/suppliers/{id}: StoreOfficer
  // Reports
  | "reports.view" // GET /api/reports/bookings: Librarian, Admin
  | "reports.rooms" // GET /api/reports/room-usage: Librarian
  | "reports.consumables" // GET /api/reports/consumable-usage: StoreOfficer
  // Admin (D2: not built, so hidden in production builds)
  | "users.view"
  | "settings.view";

const ALL: StaffRole[] = ["Librarian", "StoreOfficer", "Admin"];
const L: StaffRole[] = ["Librarian"];
const LA: StaffRole[] = ["Librarian", "Admin"];
const S: StaffRole[] = ["StoreOfficer"];
const SA: StaffRole[] = ["StoreOfficer", "Admin"];
const A: StaffRole[] = ["Admin"];

/** PLAN.md D2: Users and Settings are not built; production builds hide them entirely. */
const ADMIN_PREVIEW: StaffRole[] = import.meta.env.PROD ? [] : A;

export const PERMISSIONS: Record<Action, StaffRole[]> = {
  "dashboard.view": ALL,
  "approvals.view": L,
  "workflows.view": L,
  "auditLog.view": A,
  "requests.view": L,
  "payments.record": L,
  "students.view": LA,
  "students.edit": A,
  "rooms.view": LA,
  "rooms.create": LA,
  "rooms.edit": LA,
  "rooms.deactivate": A,
  "rooms.equipment": L,
  "equipment.view": LA,
  "equipment.create": LA,
  "equipment.edit": L,
  "maintenance.view": LA,
  "maintenance.edit": L,
  "consumables.view": SA,
  "consumables.create": SA,
  "consumables.edit": S,
  "consumables.deactivate": SA,
  "consumables.stockIn": S,
  "consumables.ledger": SA,
  "lowStock.view": S,
  "reservations.view": ["StoreOfficer", "Librarian", "Admin"],
  "reservations.act": S,
  "suppliers.view": SA,
  "suppliers.create": SA,
  "suppliers.edit": S,
  "reports.view": LA,
  "reports.rooms": L,
  "reports.consumables": S,
  "users.view": ADMIN_PREVIEW,
  "settings.view": ADMIN_PREVIEW,
};

export function can(role: StaffRole | null | undefined, action: Action): boolean {
  return role != null && PERMISSIONS[action].includes(role);
}

/** The roles allowed an action, for ProtectedRoute's `allow`. */
export function rolesFor(action: Action): StaffRole[] {
  return PERMISSIONS[action];
}
