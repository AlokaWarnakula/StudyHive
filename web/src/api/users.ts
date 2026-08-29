/**
 * Shared/foundation — user administration API client. SCAFFOLD.
 *
 * Backs W-25 Users & roles. Not part of the plan's §11 API table: it exists because the reference
 * draws the screen. Maintained by S1 along with the rest of the foundation, not by S2/S3/S4.
 *
 * The endpoints return 501 until `api/src/StudyHive.Api/Controllers/Admin/UsersController.cs` is
 * implemented. Nothing here fabricates data.
 *
 * One rule the reference itself imposes: a role change writes an audit row, so the dialog requires
 * a reason before it will save. Keep `reason` required in `changeUserRole` — that is not optional
 * politeness, it is what makes the audit entry meaningful.
 */

import { apiFetch } from "./client";
import type { PagedResult } from "./bookingRequests";

export type { PagedResult } from "./bookingRequests";

export type UserRole = "Student" | "Librarian" | "StoreOfficer" | "Admin";

export interface UserSummary {
  id: string;
  email: string;
  fullName: string;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
}

export interface ListUsersParams {
  page?: number;
  pageSize?: number;
  search?: string;
  role?: UserRole;
  sortBy?: string;
  sortDir?: "asc" | "desc";
}

function buildQuery(params: object): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params) as [string, string | number | undefined][]) {
    if (value !== undefined && value !== "") search.set(key, String(value));
  }
  const qs = search.toString();
  return qs ? `?${qs}` : "";
}

// TODO(foundation): implement GET /api/users
export function listUsers(token: string, params: ListUsersParams = {}): Promise<PagedResult<UserSummary>> {
  return apiFetch(`/api/users${buildQuery(params)}`, { token });
}

/** `reason` is required: the change writes an audit row and an empty reason makes it useless. */
// TODO(foundation): implement PUT /api/users/{id}/role
export function changeUserRole(token: string, id: string, role: UserRole, reason: string): Promise<UserSummary> {
  return apiFetch(`/api/users/${id}/role`, { method: "PUT", token, body: { role, reason } });
}

// TODO(foundation): implement PUT /api/users/{id}/status
export function setUserActive(token: string, id: string, isActive: boolean): Promise<UserSummary> {
  return apiFetch(`/api/users/${id}/status`, { method: "PUT", token, body: { isActive } });
}
