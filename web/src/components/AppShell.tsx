import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { logout as logoutRequest } from "../api/auth";
import { useAuthStore, type StaffRole } from "../store/authStore";
import { Icon, type IconName } from "./Icon";

/**
 * The console chrome from the reference document: a 228px sidebar (brand, Dashboard, then the
 * labelled groups Work / Rooms / Store / Insight, with Users and Settings pushed to the bottom)
 * beside a page area that each screen fills with its own topbar and body.
 *
 * Nav entries carry the same `allow` lists as their routes in App.tsx, so a role never sees a link
 * that would only redirect it away.
 */

const LIBRARIAN: StaffRole[] = ["Librarian", "Admin"];
const STORE: StaffRole[] = ["StoreOfficer", "Admin"];
const ADMIN_ONLY: StaffRole[] = ["Admin"];
const ALL_STAFF: StaffRole[] = ["Librarian", "StoreOfficer", "Admin"];

interface NavEntry {
  to: string;
  label: string;
  icon: IconName;
  allow: StaffRole[];
  end?: boolean;
}

interface NavGroup {
  caption?: string;
  items: NavEntry[];
  foot?: boolean;
}

const NAV: NavGroup[] = [
  { items: [{ to: "/", label: "Dashboard", icon: "layout-dashboard", allow: ALL_STAFF, end: true }] },
  {
    caption: "Work",
    items: [
      { to: "/approvals", label: "Approvals", icon: "inbox", allow: LIBRARIAN },
      { to: "/requests", label: "Requests", icon: "file-text", allow: LIBRARIAN },
      { to: "/students", label: "Students", icon: "graduation-cap", allow: LIBRARIAN },
    ],
  },
  {
    caption: "Rooms",
    items: [
      { to: "/rooms", label: "Rooms", icon: "door-open", allow: LIBRARIAN, end: true },
      { to: "/equipment", label: "Equipment", icon: "projector", allow: LIBRARIAN },
      { to: "/maintenance", label: "Maintenance", icon: "wrench", allow: LIBRARIAN },
    ],
  },
  {
    caption: "Store",
    items: [
      { to: "/consumables", label: "Consumables", icon: "package", allow: STORE, end: true },
      { to: "/reservations", label: "Reservations", icon: "clipboard-list", allow: STORE },
      { to: "/suppliers", label: "Suppliers", icon: "truck", allow: STORE },
    ],
  },
  {
    caption: "Insight",
    items: [
      { to: "/reports", label: "Reports", icon: "bar-chart-3", allow: LIBRARIAN, end: true },
      { to: "/workflows", label: "Workflow runs", icon: "workflow", allow: LIBRARIAN, end: true },
      { to: "/audit-log", label: "Audit log", icon: "scroll-text", allow: LIBRARIAN },
    ],
  },
  {
    foot: true,
    items: [
      { to: "/users", label: "Users", icon: "users", allow: ADMIN_ONLY },
      { to: "/settings", label: "Settings", icon: "settings", allow: ADMIN_ONLY },
    ],
  },
];

export function AppShell() {
  const user = useAuthStore((s) => s.user);
  const role = user?.role;

  const groups = NAV.map((g) => ({
    ...g,
    items: g.items.filter((i) => (role ? i.allow.includes(role) : false)),
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
 * with their own buttons pass `actions`. The signed-in identity sits on the right of every screen,
 * matching the dashboard and approval-queue frames.
 */
export function Screen({
  title,
  crumb,
  onBack,
  actions,
  showUser = true,
  children,
}: {
  title: string;
  crumb?: string;
  onBack?: () => void;
  actions?: React.ReactNode;
  showUser?: boolean;
  children: React.ReactNode;
}) {
  const user = useAuthStore((s) => s.user);
  const refreshToken = useAuthStore((s) => s.refreshToken);
  const storeLogout = useAuthStore((s) => s.logout);
  const navigate = useNavigate();

  async function handleLogout() {
    if (refreshToken) {
      // Best-effort — the token is memory-only anyway, so a failed revoke call doesn't leave a
      // usable session lying around client-side.
      await logoutRequest(refreshToken).catch(() => undefined);
    }
    storeLogout();
    navigate("/login", { replace: true });
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
        {showUser && user && (
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
