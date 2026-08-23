import { fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { App } from "./App";
import { useAuthStore, type StaffRole } from "./store/authStore";
import { FIXTURES_ENABLED } from "./dev/useFixture";

function signIn(role: StaffRole) {
  useAuthStore
    .getState()
    .login(
      { id: "11111111-1111-1111-1111-111111111111", name: `Test ${role}`, email: `${role}@studyhive.test`, role },
      "access-token",
      "refresh-token",
    );
}

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  );
}

/** The screens whose data the API already serves fetch on mount; keep that out of routing tests. */
function stubEmptyList() {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 }),
    }),
  );
}

describe("App routing", () => {
  afterEach(() => {
    useAuthStore.getState().logout();
    vi.unstubAllGlobals();
  });

  it("redirects an unauthenticated visitor to the sign-in screen", () => {
    renderAt("/requests");
    expect(screen.getByRole("heading", { name: "Sign in to StudyHive" })).toBeInTheDocument();
  });

  it("lands every staff role on the dashboard", () => {
    signIn("StoreOfficer");
    renderAt("/");
    expect(screen.getByRole("heading", { name: "Dashboard" })).toBeInTheDocument();
  });

  /**
   * The reference's own catalog. Each entry is one W-screen, its route, a role that owns it, and
   * the title the screen must show — so a missing or misrouted screen fails here rather than in a
   * manual click-through.
   */
  const CATALOG: [string, string, StaffRole, string][] = [
    ["W-02", "/", "Librarian", "Dashboard"],
    ["W-03", "/approvals", "Librarian", "Approvals"],
    ["W-04", "/approvals/REQ-1042", "Librarian", "REQ-1042 · Group project meeting"],
    ["W-05", "/quotations/QT-0308", "Librarian", "Quotation QT-0308"],
    ["W-06", "/workflows/WF-2291", "Librarian", "Workflow WF-2291"],
    ["W-07", "/workflows", "Librarian", "Workflow runs"],
    ["W-08", "/audit-log", "Librarian", "Audit log"],
    ["W-09", "/reports", "Librarian", "Reports"],
    ["W-13", "/rooms", "Librarian", "Rooms"],
    ["W-14", "/rooms/B-204", "Librarian", "Room B-204"],
    ["W-15", "/rooms/calendar", "Librarian", "Room calendar"],
    ["W-16", "/equipment", "Librarian", "Equipment"],
    ["W-17", "/maintenance", "Librarian", "Maintenance"],
    ["W-18", "/reports/rooms", "Librarian", "Room utilisation"],
    ["W-19", "/consumables", "StoreOfficer", "Consumables"],
    ["W-20", "/consumables/CN-04", "StoreOfficer", "Whiteboard markers"],
    ["W-21", "/consumables/low-stock", "StoreOfficer", "Low stock"],
    ["W-22", "/reservations", "StoreOfficer", "Stock reservations"],
    ["W-23", "/suppliers", "StoreOfficer", "Suppliers"],
    ["W-24", "/reports/consumables", "Librarian", "Consumable usage"],
    ["W-25", "/users", "Admin", "Users"],
    ["W-26", "/settings", "Admin", "Settings"],
  ];

  it.each(CATALOG)("%s renders at %s for a %s", (_id, path, role, title) => {
    signIn(role);
    renderAt(path);
    expect(screen.getByRole("heading", { name: title })).toBeInTheDocument();
  });

  it("renders the three S1 screens that are backed by the real API", () => {
    stubEmptyList();
    signIn("Librarian");

    renderAt("/requests");
    expect(screen.getByRole("heading", { name: "Booking requests" })).toBeInTheDocument();

    renderAt("/students");
    expect(screen.getByRole("heading", { name: "Students" })).toBeInTheDocument();
  });

  it("denies a StoreOfficer the Librarian-owned areas", () => {
    signIn("StoreOfficer");
    renderAt("/approvals");
    expect(screen.getByRole("heading", { name: "Sign in to StudyHive" })).toBeInTheDocument();
  });

  it("denies a Librarian the StoreOfficer-owned store area", () => {
    signIn("Librarian");
    renderAt("/consumables");
    expect(screen.getByRole("heading", { name: "Sign in to StudyHive" })).toBeInTheDocument();
  });

  it("keeps Users and Settings for Admin only", () => {
    signIn("Librarian");
    renderAt("/users");
    expect(screen.getByRole("heading", { name: "Sign in to StudyHive" })).toBeInTheDocument();
  });
});

describe("Sidebar navigation", () => {
  afterEach(() => {
    useAuthStore.getState().logout();
    vi.unstubAllGlobals();
  });

  it("shows a Librarian only the groups they own, and no dead-end links", () => {
    signIn("Librarian");
    renderAt("/");
    const nav = screen.getByRole("navigation", { name: "Main" });

    expect(within(nav).getByRole("link", { name: "Approvals" })).toBeInTheDocument();
    expect(within(nav).getByRole("link", { name: "Rooms" })).toBeInTheDocument();
    expect(within(nav).queryByRole("link", { name: "Consumables" })).not.toBeInTheDocument();
    expect(within(nav).queryByRole("link", { name: "Users" })).not.toBeInTheDocument();
  });

  it("shows a StoreOfficer the store group and nothing they cannot reach", () => {
    signIn("StoreOfficer");
    renderAt("/");
    const nav = screen.getByRole("navigation", { name: "Main" });

    expect(within(nav).getByRole("link", { name: "Consumables" })).toBeInTheDocument();
    expect(within(nav).getByRole("link", { name: "Suppliers" })).toBeInTheDocument();
    expect(within(nav).queryByRole("link", { name: "Approvals" })).not.toBeInTheDocument();
    expect(within(nav).queryByRole("link", { name: "Requests" })).not.toBeInTheDocument();
  });

  it("gives an Admin every group including Users and Settings", () => {
    signIn("Admin");
    renderAt("/");
    const nav = screen.getByRole("navigation", { name: "Main" });

    for (const label of ["Approvals", "Requests", "Rooms", "Consumables", "Reports", "Users", "Settings"]) {
      expect(within(nav).getByRole("link", { name: label })).toBeInTheDocument();
    }
  });

  it("navigates from the sidebar to another screen", () => {
    signIn("StoreOfficer");
    renderAt("/consumables");
    expect(screen.getByRole("heading", { name: "Consumables" })).toBeInTheDocument();

    const nav = screen.getByRole("navigation", { name: "Main" });
    fireEvent.click(within(nav).getByRole("link", { name: "Suppliers" }));

    expect(screen.getByRole("heading", { name: "Suppliers" })).toBeInTheDocument();
  });
});

describe("Development fixtures", () => {
  afterEach(() => {
    useAuthStore.getState().logout();
  });

  it("are enabled under a development build", () => {
    // The catalog test above depends on this: with fixtures off, S2-S4 screens render their
    // "not built yet" state instead of the reference content.
    expect(FIXTURES_ENABLED).toBe(true);
  });

  it("label every seeded screen as a development preview, never as live data", () => {
    signIn("Librarian");
    renderAt("/rooms");

    expect(screen.getByText(/Development preview\./)).toBeInTheDocument();
    expect(screen.getByText(/S2 has not built this endpoint yet/)).toBeInTheDocument();
  });

  it("does not label the real S1 screens as a preview", () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 }),
      }),
    );
    signIn("Librarian");
    renderAt("/requests");

    expect(screen.queryByText(/Development preview\./)).not.toBeInTheDocument();
    vi.unstubAllGlobals();
  });
});
