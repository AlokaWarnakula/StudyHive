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

export interface AuthTokenResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: UserResponse;
}

export function login(email: string, password: string): Promise<AuthTokenResponse> {
  return apiFetch<AuthTokenResponse>("/api/auth/login", { method: "POST", body: { email, password } });
}

export function logout(refreshToken: string): Promise<void> {
  return apiFetch<void>("/api/auth/logout", { method: "POST", body: { refreshToken } });
}
