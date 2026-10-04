import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { useAuthStore, type StaffRole } from "../store/authStore";

interface ProtectedRouteProps {
  children: ReactNode;
  allow?: StaffRole[];
}

/**
 * Redirects to /login when unauthenticated, keeping the page asked for as `?next=` so sign-in can
 * return there (CW-09); and to /login when the user's role isn't in `allow`.
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
    return <Navigate to="/login" replace />;
  }

  return <>{children}</>;
}

