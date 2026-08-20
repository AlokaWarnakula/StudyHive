import { create } from "zustand";

export type StaffRole = "Librarian" | "StoreOfficer" | "Admin";

export interface AuthUser {
  id: string;
  name: string;
  email: string;
  role: StaffRole;
}

interface AuthState {
  user: AuthUser | null;
  accessToken: string | null;
  refreshToken: string | null;
  login: (user: AuthUser, accessToken: string, refreshToken: string) => void;
  logout: () => void;
}

/**
 * Single Zustand store, no providers/reducers (ADR-1). No persist middleware — access and
 * refresh tokens are memory-only by design (security review: web tokens must never touch
 * localStorage/sessionStorage, unlike the Flutter client's secure storage). A page reload means
 * signing in again; that trade-off was already accepted for the staff dashboard.
 */
export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  accessToken: null,
  refreshToken: null,
  login: (user, accessToken, refreshToken) => set({ user, accessToken, refreshToken }),
  logout: () => set({ user: null, accessToken: null, refreshToken: null }),
}));
