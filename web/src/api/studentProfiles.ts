import { apiFetch } from "./client";
import type { PagedResult } from "./bookingRequests";

export type { PagedResult } from "./bookingRequests";

export interface StudentProfile {
  id: string;
  userId: string;
  /** CW-08: who the profile belongs to. */
  fullName: string;
  email: string;
  studentNumber: string;
  department: string;
  yearOfStudy: number;
  maxBookingsPerWeek: number;
  penaltyPoints: number;
  suspendedUntil: string | null;
  /** Suspended today, by the same rule eligibility uses. */
  isSuspended: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

/** Body of PUT /api/student-profiles/{id} — Admin only (see UpdateStudentProfileRequest). */
export interface UpdateStudentProfileRequest {
  department: string;
  yearOfStudy: number;
  maxBookingsPerWeek: number;
  penaltyPoints: number;
  suspendedUntil: string | null;
  isActive: boolean;
}

export interface Eligibility {
  eligible: boolean;
  reasons: string[];
  usedThisWeek: number;
  maxBookingsPerWeek: number;
}

export interface ListStudentProfilesParams {
  page?: number;
  pageSize?: number;
  search?: string;
}

function buildQuery(params: object): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params) as [string, string | number | undefined][]) {
    if (value !== undefined && value !== "") search.set(key, String(value));
  }
  const qs = search.toString();
  return qs ? `?${qs}` : "";
}

export function listStudentProfiles(
  token: string,
  params: ListStudentProfilesParams = {},
): Promise<PagedResult<StudentProfile>> {
  return apiFetch(`/api/student-profiles${buildQuery(params)}`, { token });
}

export function getStudentProfile(token: string, id: string): Promise<StudentProfile> {
  return apiFetch(`/api/student-profiles/${id}`, { token });
}

export function updateStudentProfile(
  token: string,
  id: string,
  body: UpdateStudentProfileRequest,
): Promise<StudentProfile> {
  return apiFetch(`/api/student-profiles/${id}`, { method: "PUT", token, body });
}

export function getEligibility(token: string, id: string): Promise<Eligibility> {
  return apiFetch(`/api/student-profiles/${id}/eligibility`, { token });
}
