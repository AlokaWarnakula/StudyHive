/**
 * S4 — Costing, Validation, Approval & Audit API client. SCAFFOLD.
 *
 * Typed and written; the endpoints behind it return 501 until S4 implements
 * `api/src/StudyHive.Api/Controllers/Approvals/`. Nothing here fabricates data.
 *
 * Screens this backs: W-03 Approval queue, W-04 Review proposal, W-05 Quotation detail,
 * W-06 Workflow execution viewer, W-07 Execution history, W-08 Audit log, W-09 Reports;
 * mobile M-08 Your quotation.
 *
 * `submitApprovalDecision` is the one that matters. The plan requires that an Approved decision
 * commits rooms and stock in ONE database transaction — the room exclusion constraint and the
 * stock CHECK are the last line of defence inside it. Do not split it into two calls.
 *
 * Note on workflow executions: S1 owns the rows and writes them during orchestration. These are
 * the staff-facing read views over them, which is S4's half.
 */

import { apiFetch } from "./client";
import type { PagedResult } from "./bookingRequests";

export type { PagedResult } from "./bookingRequests";

export type ApprovalDecisionKind = "Approved" | "Rejected" | "RevisionRequested";

export interface ApprovalDecision {
  id: string;
  bookingRequestId: string;
  quotationId: string | null;
  decision: ApprovalDecisionKind;
  decidedBy: string;
  reason: string | null;
  decidedAt: string;
}

export interface QuotationLineItem {
  id: string;
  description: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface Quotation {
  id: string;
  bookingRequestId: string;
  total: number;
  budgetSnapshot: number;
  createdAt: string;
  lineItems: QuotationLineItem[];
}

export interface WorkflowExecutionSummary {
  id: string;
  bookingRequestId: string;
  status: string;
  currentStep: number;
  totalSteps: number | null;
  errorCode: string | null;
  startedAt: string;
  completedAt: string | null;
}

export interface AuditLogEntry {
  id: string;
  action: string;
  entityName: string;
  entityId: string | null;
  userId: string | null;
  createdAt: string;
}

export interface ListParams {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: string;
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

// TODO(S4): implement GET /api/approvals
export function listApprovals(token: string, params: ListParams = {}): Promise<PagedResult<ApprovalDecision>> {
  return apiFetch(`/api/approvals${buildQuery(params)}`, { token });
}

// TODO(S4): implement GET /api/approvals/{id}
export function getApproval(token: string, id: string): Promise<ApprovalDecision> {
  return apiFetch(`/api/approvals/${id}`, { token });
}

/** ONE transaction: books the rooms and reserves the stock together, or neither. */
// TODO(S4): implement POST /api/approvals
export function submitApprovalDecision(
  token: string,
  body: { bookingRequestId: string; decision: ApprovalDecisionKind; reason?: string },
): Promise<ApprovalDecision> {
  return apiFetch(`/api/approvals`, { method: "POST", token, body });
}

// TODO(S4): implement GET /api/quotations
export function listQuotations(token: string, params: ListParams = {}): Promise<PagedResult<Quotation>> {
  return apiFetch(`/api/quotations${buildQuery(params)}`, { token });
}

// TODO(S4): implement GET /api/quotations/{id}
export function getQuotation(token: string, id: string): Promise<Quotation> {
  return apiFetch(`/api/quotations/${id}`, { token });
}

// TODO(S4): implement GET /api/workflow-executions
export function listWorkflowExecutions(
  token: string,
  params: ListParams = {},
): Promise<PagedResult<WorkflowExecutionSummary>> {
  return apiFetch(`/api/workflow-executions${buildQuery(params)}`, { token });
}

// TODO(S4): implement GET /api/workflow-executions/{id}
export function getWorkflowExecution(token: string, id: string): Promise<WorkflowExecutionSummary> {
  return apiFetch(`/api/workflow-executions/${id}`, { token });
}

/** Tool inputs, outputs, validation results and timings — never chain-of-thought. */
// TODO(S4): implement GET /api/workflow-executions/{id}/steps
export function getWorkflowSteps(token: string, id: string): Promise<unknown[]> {
  return apiFetch(`/api/workflow-executions/${id}/steps`, { token });
}

// TODO(S4): implement GET /api/audit-logs
export function listAuditLogs(token: string, params: ListParams = {}): Promise<PagedResult<AuditLogEntry>> {
  return apiFetch(`/api/audit-logs${buildQuery(params)}`, { token });
}

// TODO(S4): implement GET /api/reports/bookings
export function getBookingsReport(token: string, from: string, to: string): Promise<unknown> {
  return apiFetch(`/api/reports/bookings${buildQuery({ from, to })}`, { token });
}

// TODO(S2): implement GET /api/reports/room-usage
export function getRoomUsageReport(token: string, from: string, to: string): Promise<unknown> {
  return apiFetch(`/api/reports/room-usage${buildQuery({ from, to })}`, { token });
}

// TODO(S3): implement GET /api/reports/consumable-usage
export function getConsumableUsageReport(token: string, from: string, to: string): Promise<unknown> {
  return apiFetch(`/api/reports/consumable-usage${buildQuery({ from, to })}`, { token });
}
