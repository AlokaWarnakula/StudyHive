import { apiFetch } from "./client";
import type { PagedResult } from "./bookingRequests";

export type { PagedResult } from "./bookingRequests";

export interface StudentProfile {
  id: string;
  userId: string;
  studentNumber: string;
  department: string;
  yearOfStudy: number;
  maxBookingsPerWeek: number;
  penaltyPoints: number;
  suspendedUntil: string | null;
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
