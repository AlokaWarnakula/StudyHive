import { describe, expect, it } from "vitest";
import { can, PERMISSIONS, type Action } from "./permissions";
import type { StaffRole } from "../store/authStore";

/** CW-05: the role table, checked per role against the API attributes it mirrors (after B1). */
const expectations: Record<StaffRole, { allowed: Action[]; forbidden: Action[] }> = {
  Librarian: {
    allowed: ["approvals.view", "requests.view", "students.view", "rooms.create", "rooms.equipment", "equipment.edit",
      "maintenance.view", "maintenance.edit", "reservations.view", "reports.view", "reports.rooms"],
    forbidden: ["students.edit", "rooms.deactivate", "reservations.act", "suppliers.view", "consumables.view",
      "consumables.ledger", "auditLog.view", "reports.consumables", "lowStock.view"],
  },
  StoreOfficer: {
    allowed: ["consumables.view", "consumables.create", "consumables.edit", "consumables.deactivate", "consumables.stockIn",
      "consumables.ledger", "lowStock.view", "reservations.view", "reservations.act", "suppliers.view",
      "suppliers.create", "suppliers.edit", "reports.consumables"],
    forbidden: ["requests.view", "students.view", "rooms.view", "maintenance.view", "approvals.view", "reports.view",
      "auditLog.view"],
  },
  Admin: {
    allowed: ["students.view", "students.edit", "rooms.create", "rooms.deactivate", "equipment.create", "maintenance.view",
      "consumables.create", "consumables.deactivate", "consumables.ledger", "reservations.view", "suppliers.view",
      "suppliers.create", "reports.view", "auditLog.view"],
    // The API refuses Admin these, so the web must not offer them (CW-05).
    forbidden: ["requests.view", "approvals.view", "workflows.view", "maintenance.edit", "rooms.equipment",
      "equipment.edit", "consumables.edit", "consumables.stockIn", "reservations.act", "suppliers.edit",
      "lowStock.view", "reports.rooms", "reports.consumables"],
  },
};

describe("permissions (CW-05)", () => {
  for (const [role, { allowed, forbidden }] of Object.entries(expectations) as [StaffRole, (typeof expectations)[StaffRole]][]) {
    it(`matches the API for ${role}`, () => {
      for (const action of allowed) expect(can(role, action), `${role} ${action}`).toBe(true);
      for (const action of forbidden) expect(can(role, action), `${role} ${action}`).toBe(false);
    });
  }

  it("allows nothing without a signed-in role", () => {
    for (const action of Object.keys(PERMISSIONS) as Action[]) expect(can(null, action)).toBe(false);
  });

  it("shows Users and Settings to Admin only, and only outside production builds (D2)", () => {
    expect(PERMISSIONS["users.view"]).toEqual(import.meta.env.PROD ? [] : ["Admin"]);
    expect(PERMISSIONS["settings.view"]).toEqual(import.meta.env.PROD ? [] : ["Admin"]);
  });
});
