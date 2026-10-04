import { apiFetch } from "./client";
import type { StaffRole } from "../store/authStore";

export type UserRole = StaffRole | "Student";

export interface UserResponse {
  id: string;
  email: string;
  fullName: string;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
}

/** With `?client=web` the refresh token is set as an httpOnly cookie and left out of this body (PLAN.md D1). */
export interface AuthTokenResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
  user: UserResponse;
}

export function login(email: string, password: string): Promise<AuthTokenResponse> {
  return apiFetch<AuthTokenResponse>("/api/auth/login?client=web", { method: "POST", body: { email, password } });
}

/** Revokes the refresh cookie's token and clears the cookie. The JSON body is required (CSRF guard). */
export function logout(): Promise<void> {
  return apiFetch<void>("/api/auth/logout?client=web", { method: "POST", body: {} });
}
