/**
 * S4 — Costing, Validation, Approval & Audit API client, typed against the live controllers in
 * `api/src/StudyHive.Api/Controllers/Approvals/` (response classes at the bottom of each file).
 *
 * Screens this backs: W-03 Approval queue, W-04 Review proposal, W-05 Quotation detail,
 * W-06 Workflow execution viewer, W-07 Execution history, W-08 Audit log, W-09 Reports;
 * mobile M-08 Your quotation.
 *
 * `submitApprovalDecision` is the one that matters. An Approved decision books the rooms and
 * reserves the stock in ONE database transaction on the server — the room exclusion constraint and
 * the stock CHECK are the last line of defence inside it. A clash comes back as a 409 whose
 * ProblemDetails `type` says why; see `approvalConflictKind`.
 *
 * Roles: approvals, quotation list and workflow executions are Librarian; a single quotation is
 * Librarian or the owning Student; audit logs are Admin; the bookings report is Librarian or Admin.
 * Room usage is S2's (`getRoomUsageReport` in ./rooms.ts), consumable usage S3's
 * (`getConsumableUsageReport` in ./consumables.ts).
 */

import { ApiError, apiFetch } from "./client";
import type { PagedResult } from "./bookingRequests";

export type { PagedResult } from "./bookingRequests";

export type ApprovalDecisionKind = "Approved" | "Rejected" | "RevisionRequested";

/** The queue filter: Pending (awaiting a decision) or the decision made. */
export type ApprovalQueueStatus = "Pending" | ApprovalDecisionKind;

export type QuotationStatus = "Draft" | "Proposed" | "Approved" | "Rejected" | "Superseded";

export type WorkflowStatus =
  | "Started"
  | "InProgress"
  | "PendingApproval"
  | "Approved"
  | "Rejected"
  | "Failed"
  | "Completed";

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

/** ApprovalDecisionResponse — mirrors `approval_decisions`. */
export interface ApprovalDecision {
  id: string;
  quotationId: string;
  decidedBy: string;
  /** The role held at the time of the decision, stored so a later role change cannot rewrite history. */
  decidedByRole: string;
  decision: ApprovalDecisionKind;
  comments: string | null;
  decidedAt: string;
}

/** POST /api/approvals body. `comments` is required unless the decision is Approved (400 otherwise). */
export interface ApprovalDecisionRequest {
  quotationId: string;
  decision: ApprovalDecisionKind;
  comments?: string | null;
}

/** ApprovalQueueItemResponse — one queue row: a quotation and its latest decision, if any. */
export interface ApprovalQueueItem {
  quotationId: string;
  bookingRequestId: string;
  studentId: string;
  objective: string;
  groupSize: number;
  version: number;
  roomFee: number;
  consumableCost: number;
  totalAmount: number;
  budgetSnapshot: number;
  withinBudget: boolean;
  currency: string;
  quotationStatus: QuotationStatus;
  createdAt: string;
  decision: ApprovalDecision | null;
  /** Pending while the quotation is Proposed, otherwise the latest decision. */
  status: ApprovalQueueStatus | QuotationStatus;
}

/** ApprovalLineItemResponse. `itemType` is Room or Consumable. */
export interface ApprovalLineItem {
  id: string;
  itemType: "Room" | "Consumable";
  itemName: string;
  roomId: string | null;
  /** Null until an approval books the room. */
  roomBookingId: string | null;
  consumableId: string | null;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

/** ApprovalDetailResponse — GET /api/approvals/{quotationId}. */
export interface ApprovalDetail {
  item: ApprovalQueueItem;
  lineItems: ApprovalLineItem[];
}

/**
 * QuotationLineItemResponse — mirrors `quotation_line_items`. A Room line carries `roomId` (and
 * `roomBookingId` once approved); a Consumable line carries `consumableId`.
 */
export interface QuotationLineItem {
  id: string;
  quotationId: string;
  itemType: "Room" | "Consumable";
  roomId: string | null;
  roomBookingId: string | null;
  consumableId: string | null;
  itemName: string;
  quantity: number;
  unitPrice: number;
  /** READ-ONLY. A generated column: `quantity * unit_price`. */
  lineTotal: number;
  createdAt: string;
}

/** QuotationResponse — GET /api/quotations/{id}, mirrors `quotations` with its lines. */
export interface Quotation {
  id: string;
  bookingRequestId: string;
  version: number;
  roomFee: number;
  consumableCost: number;
  /** READ-ONLY. A generated column: `room_fee + consumable_cost`. */
  totalAmount: number;
  /** The budget as it stood when the quote was made, copied rather than joined. */
  budgetSnapshot: number;
  /** READ-ONLY. A generated column: `total_amount <= budget_snapshot`. */
  withinBudget: boolean;
  currency: string;
  status: QuotationStatus;
  createdAt: string;
  updatedAt: string;
  lineItems: QuotationLineItem[];
}

/** QuotationSummaryResponse — one row of GET /api/quotations (no lines, just their count). */
export interface QuotationSummary {
  id: string;
  bookingRequestId: string;
  objective: string;
  version: number;
  roomFee: number;
  consumableCost: number;
  totalAmount: number;
  budgetSnapshot: number;
  withinBudget: boolean;
  currency: string;
  status: QuotationStatus;
  lineItemCount: number;
  createdAt: string;
  updatedAt: string;
}

/** WorkflowExecutionSummaryResponse — mirrors `workflow_executions`. */
export interface WorkflowExecutionSummary {
  id: string;
  bookingRequestId: string;
  objective: string;
  status: WorkflowStatus;
  currentStep: number;
  totalSteps: number | null;
  /** e.g. VALIDATION_FAILED, STEP_RETRY_EXHAUSTED. */
  errorCode: string | null;
  /** For VALIDATION_FAILED this is the revision note shown to the student. */
  errorMessage: string | null;
  startedAt: string;
  completedAt: string | null;
  updatedAt: string;
}

/** WorkflowStepResponse — tool inputs, outputs, validation results and timings, never chain-of-thought. */
export interface WorkflowStep {
  id: string;
  stepNumber: number;
  attempt: number;
  agentName: string;
  toolName: string | null;
  /** Stored JSON, parsed. A value that was not JSON comes back as a string. */
  input: unknown;
  output: unknown;
  validationResult: "Pass" | "Fail" | "Warning" | null;
  validationDetails: string | null;
  errorMessage: string | null;
  durationMs: number | null;
  createdAt: string;
}

/** WorkflowExecutionDetailResponse — GET /api/workflow-executions/{id}. */
export interface WorkflowExecutionDetail {
  execution: WorkflowExecutionSummary;
  plan: unknown;
  steps: WorkflowStep[];
}

/** AuditLogResponse — mirrors `audit_logs`. Append-only: there is deliberately no write function here. */
export interface AuditLogEntry {
  id: string;
  /** Null when the acting account was deleted: the FK is ON DELETE SET NULL so the trail outlives it. */
  userId: string | null;
  userEmail: string | null;
  correlationId: string | null;
  action: string;
  entityType: string;
  entityId: string;
  details: unknown;
  ipAddress: string | null;
  createdAt: string;
}

/** BookingsReportResponse — GET /api/reports/bookings. */
export interface BookingsReport {
  from: string;
  to: string;
  totalRequests: number;
  byStatus: { status: BookingRequestStatus; count: number }[];
  /** Monday-starting weeks (Asia/Colombo); `weekStart` is a date (yyyy-MM-dd). */
  byWeek: { weekStart: string; requests: number; approved: number; approvedSpend: number; approvedBudget: number }[];
  spend: {
    approvedQuotations: number;
    totalSpend: number;
    totalBudget: number;
    withinBudget: number;
    overBudget: number;
  };
}

export interface ListParams {
  page?: number;
  pageSize?: number;
  search?: string;
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

/** GET /api/approvals — Pending first. `sortBy`: createdAt | totalAmount. */
export function listApprovals(
  token: string,
  params: ListParams & { status?: ApprovalQueueStatus } = {},
): Promise<PagedResult<ApprovalQueueItem>> {
  return apiFetch(`/api/approvals${buildQuery(params)}`, { token });
}

/** GET /api/approvals/{quotationId} — keyed by quotation, not by decision. */
export function getApproval(token: string, quotationId: string): Promise<ApprovalDetail> {
  return apiFetch(`/api/approvals/${quotationId}`, { token });
}

/** POST /api/approvals — 201 with the decision. ONE transaction: books the rooms and reserves the stock together, or neither. */
export function submitApprovalDecision(token: string, body: ApprovalDecisionRequest): Promise<ApprovalDecision> {
  return apiFetch(`/api/approvals`, { method: "POST", token, body });
}

/** Why a decision came back 409, from the ProblemDetails `type` (https://studyhive.dev/errors/{kind}). */
export type ApprovalConflictKind =
  | "already-decided"
  | "room-conflict"
  | "insufficient-stock"
  | "proposal-incomplete"
  | "conflict";

/** The conflict kind of a failed `submitApprovalDecision`, or null when the error is not a 409. */
export function approvalConflictKind(error: unknown): ApprovalConflictKind | null {
  if (!(error instanceof ApiError) || error.status !== 409) return null;
  const kind = error.problem.type?.split("/").pop();
  switch (kind) {
    case "already-decided":
    case "room-conflict":
    case "insufficient-stock":
    case "proposal-incomplete":
      return kind;
    default:
      return "conflict";
  }
}

/** GET /api/quotations (Librarian). `sortBy`: createdAt | totalAmount | status. */
export function listQuotations(
  token: string,
  params: ListParams & { status?: QuotationStatus; bookingRequestId?: string } = {},
): Promise<PagedResult<QuotationSummary>> {
  return apiFetch(`/api/quotations${buildQuery(params)}`, { token });
}

/** GET /api/quotations/{id} — Librarian, or the Student who owns the request (403 otherwise). */
export function getQuotation(token: string, id: string): Promise<Quotation> {
  return apiFetch(`/api/quotations/${id}`, { token });
}

/** GET /api/workflow-executions — Failed first. `sortBy`: startedAt | completedAt | status. */
export function listWorkflowExecutions(
  token: string,
  params: ListParams & { status?: WorkflowStatus; errorCode?: string } = {},
): Promise<PagedResult<WorkflowExecutionSummary>> {
  return apiFetch(`/api/workflow-executions${buildQuery(params)}`, { token });
}

/** GET /api/workflow-executions/{id} — the execution, its plan and every step attempt. */
export function getWorkflowExecution(token: string, id: string): Promise<WorkflowExecutionDetail> {
  return apiFetch(`/api/workflow-executions/${id}`, { token });
}

/** GET /api/workflow-executions/{id}/steps — the step attempts alone. */
export function getWorkflowSteps(token: string, id: string): Promise<WorkflowStep[]> {
  return apiFetch(`/api/workflow-executions/${id}/steps`, { token });
}

export interface AuditLogFilters extends ListParams {
  action?: string;
  entityType?: string;
  entityId?: string;
  userId?: string;
  /** ISO date-time; `to` must be later than `from`. */
  from?: string;
  to?: string;
}

/** GET /api/audit-logs (Admin). `sortBy`: createdAt | action | entityType. */
export function listAuditLogs(token: string, params: AuditLogFilters = {}): Promise<PagedResult<AuditLogEntry>> {
  return apiFetch(`/api/audit-logs${buildQuery(params)}`, { token });
}

/** GET /api/reports/bookings (Librarian or Admin). Omitted bounds default to the last 30 days. */
export function getBookingsReport(token: string, from?: string, to?: string): Promise<BookingsReport> {
  return apiFetch(`/api/reports/bookings${buildQuery({ from, to })}`, { token });
}
