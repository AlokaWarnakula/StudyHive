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
    // Live screens title themselves from the API; until it answers they show a generic title.
    ["W-04", "/approvals/REQ-1042", "Librarian", "Review proposal"],
    ["W-05", "/quotations/QT-0308", "Librarian", "Quotation QT-0308"],
    ["W-06", "/workflows/WF-2291", "Librarian", "Workflow WF-2291"],
    ["W-07", "/workflows", "Librarian", "Workflow runs"],
    ["W-08", "/audit-log", "Admin", "Audit log"],
    ["W-09", "/reports", "Librarian", "Reports"],
    ["W-13", "/rooms", "Librarian", "Rooms"],
    ["W-14", "/rooms/B-204", "Librarian", "Room B-204"],
    ["W-15", "/rooms/calendar", "Librarian", "Room calendar"],
    ["W-16", "/equipment", "Librarian", "Equipment"],
    ["W-17", "/maintenance", "Librarian", "Maintenance"],
    ["W-18", "/reports/rooms", "Librarian", "Room utilisation"],
    ["W-19", "/consumables", "StoreOfficer", "Consumables"],
    ["W-20", "/consumables/CN-04", "StoreOfficer", "Consumable"],
    ["W-21", "/consumables/low-stock", "StoreOfficer", "Low stock"],
    ["W-22", "/reservations", "StoreOfficer", "Stock reservations"],
    ["W-23", "/suppliers", "StoreOfficer", "Suppliers"],
    ["W-24", "/reports/consumables", "StoreOfficer", "Consumable usage"],
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
    expectNotAllowed();
  });

  it("denies a Librarian the StoreOfficer-owned store area", () => {
    signIn("Librarian");
    renderAt("/consumables");
    expectNotAllowed();
  });

  // S4 screens are gated exactly as their APIs are: the audit log is Admin-only, approvals Librarian-only.
  it("denies a Librarian the Admin-only audit log", () => {
    signIn("Librarian");
    renderAt("/audit-log");
    expectNotAllowed();
  });

  it("denies an Admin the Librarian-only approvals", () => {
    signIn("Admin");
    renderAt("/approvals");
    expectNotAllowed();
  });

  it("keeps Users and Settings for Admin only", () => {
    signIn("Librarian");
    renderAt("/users");
    expectNotAllowed();
  });

  // CW-09: a signed-in user stays signed in and inside the shell, with a way back.
  it("shows Page not found inside the shell for an unknown URL while signed in", () => {
    signIn("Librarian");
    renderAt("/no-such-page");
    expect(screen.getByRole("heading", { name: "Page not found" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Go to Dashboard" })).toHaveAttribute("href", "/");
    expect(screen.getByRole("button", { name: /sign out/i })).toBeInTheDocument();
  });

  it("sends a signed-out visitor of an unknown URL to sign in, keeping it as next", () => {
    renderAt("/no-such-page");
    expect(screen.getByRole("heading", { name: "Sign in to StudyHive" })).toBeInTheDocument();
  });
});

/** CW-09: the "not allowed" page inside the shell, not a bounce to sign-in. */
function expectNotAllowed() {
  expect(screen.getByRole("heading", { name: "You don't have access" })).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "Go to Dashboard" })).toBeInTheDocument();
  expect(screen.queryByRole("heading", { name: "Sign in to StudyHive" })).not.toBeInTheDocument();
}


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

    for (const label of ["Students", "Rooms", "Maintenance", "Consumables", "Reservations", "Suppliers", "Reports", "Audit log", "Users", "Settings"]) {
      expect(within(nav).getByRole("link", { name: label })).toBeInTheDocument();
    }
    // CW-05: GET /api/booking-requests is Librarian-only, so an Admin gets no Requests link either.
    expect(within(nav).queryByRole("link", { name: "Requests" })).not.toBeInTheDocument();
    // The approvals and workflow APIs are Librarian-only, so an Admin gets no dead-end links to them.
    expect(within(nav).queryByRole("link", { name: "Approvals" })).not.toBeInTheDocument();
    expect(within(nav).queryByRole("link", { name: "Workflow runs" })).not.toBeInTheDocument();
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

  it("does not label the live S2 room screen as development preview data", () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 }),
      }),
    );
    signIn("Librarian");
    renderAt("/rooms");

    expect(screen.queryByText(/Development preview\./)).not.toBeInTheDocument();
    vi.unstubAllGlobals();
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
