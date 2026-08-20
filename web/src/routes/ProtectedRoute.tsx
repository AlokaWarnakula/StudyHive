import type { ReactNode } from "react";
import { Navigate } from "react-router-dom";
import { useAuthStore, type StaffRole } from "../store/authStore";

interface ProtectedRouteProps {
  children: ReactNode;
  allow?: StaffRole[];
}

/** Redirects to /login when unauthenticated, or when the user's role isn't in `allow`. */
export function ProtectedRoute({ children, allow }: ProtectedRouteProps) {
  const user = useAuthStore((s) => s.user);

  if (!user) {
    return <Navigate to="/login" replace />;
  }

  if (allow && !allow.includes(user.role)) {
    return <Navigate to="/login" replace />;
  }

  return <>{children}</>;
}
