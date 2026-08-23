import { apiFetch } from "./client";

export type BookingRequestStatus =
  | "Draft"
  | "Submitted"
  | "Processing"
  | "PendingApproval"
  | "Approved"
  | "Rejected"
  | "RevisionRequested"
  | "Completed"
  | "Cancelled"
  | "Failed";

export type WorkflowStatus = "Started" | "InProgress" | "PendingApproval" | "Approved" | "Rejected" | "Failed" | "Completed";

export interface BookingRequestItem {
  consumableId: string;
  quantity: number;
}

export interface BookingRequest {
  id: string;
  studentId: string;
  objective: string;
  groupSize: number;
  preferredDateFrom: string;
  preferredDateTo: string;
  preferredTimeFrom: string;
  preferredTimeTo: string;
  sessionsRequired: number;
  sessionDurationMinutes: number;
  budget: number;
  notes: string | null;
  status: BookingRequestStatus;
  items: BookingRequestItem[];
  latestWorkflowId: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface WorkflowStepLog {
  stepNumber: number;
  agentName: string;
  toolName: string | null;
  validationResult: "Pass" | "Fail" | "Warning" | null;
  errorMessage: string | null;
  durationMs: number | null;
  outputJson: string | null;
  createdAt: string;
}

export interface WorkflowStatusResponse {
  workflowId: string;
  bookingRequestId: string;
  status: WorkflowStatus;
  currentStep: number;
  totalSteps: number | null;
  errorCode: string | null;
  errorMessage: string | null;
  startedAt: string;
  completedAt: string | null;
  updatedAt: string;
  steps: WorkflowStepLog[];
}

export interface ListBookingRequestsParams {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDir?: "asc" | "desc";
  search?: string;
  status?: string;
}

function buildQuery(params: object): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params) as [string, string | number | undefined][]) {
    if (value !== undefined && value !== "") search.set(key, String(value));
  }
  const qs = search.toString();
  return qs ? `?${qs}` : "";
}

export function listBookingRequests(
  token: string,
  params: ListBookingRequestsParams = {},
): Promise<PagedResult<BookingRequest>> {
  return apiFetch(`/api/booking-requests${buildQuery(params)}`, { token });
}

export function getBookingRequest(token: string, id: string): Promise<BookingRequest> {
  return apiFetch(`/api/booking-requests/${id}`, { token });
}

export function getWorkflowStatus(token: string, id: string): Promise<WorkflowStatusResponse> {
  return apiFetch(`/api/booking-requests/${id}/status`, { token });
}
