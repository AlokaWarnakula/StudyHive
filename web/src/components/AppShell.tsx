import type { ReactNode } from "react";
import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { logout as logoutRequest } from "../api/auth";
import { useAuthStore } from "../store/authStore";

const NAV_ITEMS = [
  { to: "/requests", label: "Requests" },
  { to: "/rooms", label: "Rooms" },
  { to: "/stock", label: "Stock" },
  { to: "/admin", label: "Admin" },
];

/** Staff dashboard shell: side nav + content outlet. Business pages fill in the outlet per owner. */
export function AppShell({ children }: { children?: ReactNode }) {
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
    <div className="app-shell">
      <aside className="app-shell__nav">
        <div className="app-shell__brand">StudyHive</div>
        <nav>
          {NAV_ITEMS.map((item) => (
            <NavLink key={item.to} to={item.to} className="app-shell__link">
              {item.label}
            </NavLink>
          ))}
        </nav>
        <div className="app-shell__user">
          {user && <span>{user.name}</span>}
          <button type="button" onClick={handleLogout}>
            Sign out
          </button>
        </div>
      </aside>
      <main className="app-shell__content">{children ?? <Outlet />}</main>
    </div>
  );
}
