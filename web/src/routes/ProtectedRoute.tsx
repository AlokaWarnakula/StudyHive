import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { NotAllowedPage } from "../components/StatusPages";
import { useAuthStore, type StaffRole } from "../store/authStore";

interface ProtectedRouteProps {
  children: ReactNode;
  allow?: StaffRole[];
}

/**
 * Redirects to /login when unauthenticated, keeping the page asked for as `?next=` so sign-in can
 * return there (CW-09). A signed-in user whose role isn't in `allow` stays signed in and sees
 * "You don't have access" inside the shell (CW-09).
 */
export function ProtectedRoute({ children, allow }: ProtectedRouteProps) {
  const user = useAuthStore((s) => s.user);
  const location = useLocation();

  if (!user) {
    const next = `${location.pathname}${location.search}`;
    const to = next === "/" ? "/login" : `/login?next=${encodeURIComponent(next)}`;
    return <Navigate to={to} replace />;
  }

  if (allow && !allow.includes(user.role)) {
    return <NotAllowedPage />;
  }

  return <>{children}</>;
}

