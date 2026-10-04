import { create } from "zustand";

export type StaffRole = "Librarian" | "StoreOfficer" | "Admin";

export interface AuthUser {
  id: string;
  name: string;
  email: string;
  role: StaffRole;
}

export const SESSION_EXPIRED_MESSAGE = "Your session expired. Please sign in again.";

interface AuthState {
  user: AuthUser | null;
  accessToken: string | null;
  /** Why the user is on the sign-in screen when it was not their choice; cleared by the next sign-in. */
  signedOutReason: string | null;
  /** Bumped on every sign-in, refresh and sign-out, so a refresh that lands late can tell the session moved on. */
  epoch: number;
  login: (user: AuthUser, accessToken: string) => void;
  logout: () => void;
  /** The refresh token was rejected: sign out and say why on the sign-in screen. */
  expire: () => void;
}

/**
 * Single Zustand store, no providers/reducers (ADR-1). No persist middleware: the access token is
 * memory-only by design and never touches localStorage/sessionStorage. The refresh token lives in
 * the API's httpOnly `studyhive_refresh` cookie (PLAN.md D1), which this code never sees; on a reload
 * SessionRestore exchanges it for a new access token, so staff stay signed in.
 */
export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  accessToken: null,
  signedOutReason: null,
  epoch: 0,
  login: (user, accessToken) => set((s) => ({ user, accessToken, signedOutReason: null, epoch: s.epoch + 1 })),
  logout: () => set((s) => ({ user: null, accessToken: null, epoch: s.epoch + 1 })),
  expire: () =>
    set((s) => ({
      user: null,
      accessToken: null,
      signedOutReason: s.user ? SESSION_EXPIRED_MESSAGE : s.signedOutReason,
      epoch: s.epoch + 1,
    })),
}));
