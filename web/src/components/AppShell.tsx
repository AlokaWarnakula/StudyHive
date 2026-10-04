import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { logout as logoutRequest } from "../api/auth";
import { can, type Action } from "../auth/permissions";
import { useAuthStore } from "../store/authStore";
import { Icon, type IconName } from "./Icon";

/**
 * The console chrome from the reference document: a 228px sidebar (brand, Dashboard, then the
 * labelled groups Work / Rooms / Store / Insight, with Users and Settings pushed to the bottom)
 * beside a page area that each screen fills with its own topbar and body.
 *
 * Nav entries ask the same role table as their routes (auth/permissions.ts), so a role never sees a
 * link that would only redirect it away.
 */

interface NavEntry {
  to: string;
  label: string;
  icon: IconName;
  action: Action;
  end?: boolean;
}

interface NavGroup {
  caption?: string;
  items: NavEntry[];
  foot?: boolean;
}

const NAV: NavGroup[] = [
  { items: [{ to: "/", label: "Dashboard", icon: "layout-dashboard", action: "dashboard.view", end: true }] },
  {
    caption: "Work",
    items: [
      { to: "/approvals", label: "Approvals", icon: "inbox", action: "approvals.view" },
      { to: "/requests", label: "Requests", icon: "file-text", action: "requests.view" },
      { to: "/students", label: "Students", icon: "graduation-cap", action: "students.view" },
    ],
  },
  {
    caption: "Rooms",
    items: [
      { to: "/rooms", label: "Rooms", icon: "door-open", action: "rooms.view", end: true },
      { to: "/equipment", label: "Equipment", icon: "projector", action: "equipment.view" },
      { to: "/maintenance", label: "Maintenance", icon: "wrench", action: "maintenance.view" },
    ],
  },
  {
    caption: "Store",
    items: [
      { to: "/consumables", label: "Consumables", icon: "package", action: "consumables.view", end: true },
      { to: "/reservations", label: "Reservations", icon: "clipboard-list", action: "reservations.view" },
      { to: "/suppliers", label: "Suppliers", icon: "truck", action: "suppliers.view" },
      { to: "/reports/consumables", label: "Usage report", icon: "bar-chart-3", action: "reports.consumables" },
    ],
  },
  {
    caption: "Insight",
    items: [
      { to: "/reports", label: "Reports", icon: "bar-chart-3", action: "reports.view", end: true },
      { to: "/workflows", label: "Workflow runs", icon: "workflow", action: "workflows.view", end: true },
      { to: "/audit-log", label: "Audit log", icon: "scroll-text", action: "auditLog.view" },
    ],
  },
  {
    foot: true,
    items: [
      { to: "/users", label: "Users", icon: "users", action: "users.view" },
      { to: "/settings", label: "Settings", icon: "settings", action: "settings.view" },
    ],
  },
];

export function AppShell() {
  const user = useAuthStore((s) => s.user);
  const role = user?.role;

  const groups = NAV.map((g) => ({
    ...g,
    items: g.items.filter((i) => can(role, i.action)),
  })).filter((g) => g.items.length > 0);

  return (
    <div className="console">
      <aside className="sb">
        <div className="sb-brand">
          <Icon name="library" size={20} />
          StudyHive
        </div>
        <nav aria-label="Main" style={{ display: "contents" }}>
          {groups.map((g, gi) => (
            <div key={g.caption ?? `g${gi}`} className={g.foot ? "sb-foot" : undefined} style={{ display: "contents" }}>
              {g.caption && <div className="sbg">{g.caption}</div>}
              {g.items.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  end={item.end}
                  className={({ isActive }) => (isActive ? "sbi active" : "sbi")}
                >
                  <Icon name={item.icon} />
                  {item.label}
                </NavLink>
              ))}
            </div>
          ))}
        </nav>
      </aside>
      <div className="wmain">
        <Outlet />
      </div>
    </div>
  );
}

/**
 * One screen's topbar + body. Detail screens pass `onBack` for the reference's left arrow; screens
 * with their own buttons pass `actions`. The signed-in identity and Sign out sit on the right of
 * every screen (AUDIT CW-03: they used to be hidden on 18 pages).
 */
export function Screen({
  title,
  crumb,
  onBack,
  actions,
  children,
}: {
  title: string;
  crumb?: string;
  onBack?: () => void;
  actions?: React.ReactNode;
  children: React.ReactNode;
}) {
  const user = useAuthStore((s) => s.user);
  const storeLogout = useAuthStore((s) => s.logout);
  const navigate = useNavigate();

  async function handleLogout() {
    // Sign out locally first so a slow or failed revoke call never leaves the session usable here;
    // the call then revokes the refresh cookie's token and clears the cookie (best-effort).
    // Both in the same tick, so the route guard never sees a signed-out user on a protected page
    // (it would send a deliberate sign-out to /login?next=…).
    storeLogout();
    navigate("/login", { replace: true });
    await logoutRequest().catch(() => undefined);
  }

  return (
    <>
      <div className="wtop">
        {onBack && (
          <button type="button" className="btn btn-ghost btn-icon" onClick={onBack} aria-label="Go back">
            <Icon name="arrow-left" />
          </button>
        )}
        <div>
          <h4>{title}</h4>
          {crumb && <span className="crumb">{crumb}</span>}
        </div>
        {actions && <div style={{ marginLeft: "auto", display: "flex", gap: 10, alignItems: "center" }}>{actions}</div>}
        {user && (
          <div className="wtop-user" style={actions ? { marginLeft: 0 } : undefined}>
            <span className="tag tag-outline">{user.role === "StoreOfficer" ? "Store officer" : user.role}</span>
            <b style={{ fontSize: 14 }}>{user.name}</b>
            <button type="button" className="btn btn-secondary" onClick={handleLogout}>
              Sign out
            </button>
          </div>
        )}
      </div>
      <div className="wbody">{children}</div>
    </>
  );
}
