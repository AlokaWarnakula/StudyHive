import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "./client";
import {
  approvalConflictKind,
  getApproval,
  getBookingsReport,
  getQuotation,
  getWorkflowExecution,
  listApprovals,
  listAuditLogs,
  listQuotations,
  listWorkflowExecutions,
  submitApprovalDecision,
  type ApprovalDetail,
} from "./approvals";

const token = "test-token";

afterEach(() => vi.unstubAllGlobals());

function mockResponse(body: unknown = {}, status = 200) {
  const fetchMock = vi.fn().mockImplementation(async () => new Response(JSON.stringify(body), {
      status,
      headers: { "Content-Type": "application/json" },
    }));
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

const emptyPage = { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 };

describe("S4 approvals API client", () => {
  it("posts a decision as { quotationId, decision, comments }", async () => {
    const fetchMock = mockResponse({ id: "d1", quotationId: "q1", decision: "Rejected" }, 201);
    const body = { quotationId: "q1", decision: "Rejected" as const, comments: "Room too large for the group" };
    const result = await submitApprovalDecision(token, body);
    expect(fetchMock.mock.calls[0][0]).toBe("http://localhost:5299/api/approvals");
    expect(fetchMock.mock.calls[0][1]).toMatchObject({ method: "POST", body: JSON.stringify(body) });
    expect(fetchMock.mock.calls[0][1].headers.Authorization).toBe(`Bearer ${token}`);
    const sent = JSON.parse(fetchMock.mock.calls[0][1].body);
    expect(sent).not.toHaveProperty("bookingRequestId");
    expect(sent).not.toHaveProperty("reason");
    expect(result.quotationId).toBe("q1");
  });

  it("filters the queue by status and reads the quotation-keyed detail", async () => {
    const fetchMock = mockResponse(emptyPage);
    await listApprovals(token, { status: "Pending", page: 2, sortBy: "totalAmount", sortDir: "asc" });
    expect(fetchMock.mock.calls[0][0]).toBe(
      "http://localhost:5299/api/approvals?status=Pending&page=2&sortBy=totalAmount&sortDir=asc",
    );

    const detail: ApprovalDetail = {
      item: {
        quotationId: "q1", bookingRequestId: "r1", studentId: "s1", objective: "Revise", groupSize: 4, version: 1,
        roomFee: 0, consumableCost: 120, totalAmount: 120, budgetSnapshot: 500, withinBudget: true, currency: "LKR",
        quotationStatus: "Proposed", createdAt: "2026-09-30T00:00:00Z", decision: null, status: "Pending",
      },
      lineItems: [{
        id: "l1", itemType: "Room", itemName: "Quiet Study 101", roomId: "room1", roomBookingId: null,
        consumableId: null, quantity: 2, unitPrice: 0, lineTotal: 0,
      }],
    };
    mockResponse(detail);
    const read = await getApproval(token, "q1");
    expect(read.item.status).toBe("Pending");
    expect(read.lineItems[0].roomId).toBe("room1");
  });

  it("maps each 409 ProblemDetails type to a conflict kind", async () => {
    mockResponse({ type: "https://studyhive.dev/errors/room-conflict", title: "Room can no longer be booked", status: 409 }, 409);
    const error = await submitApprovalDecision(token, { quotationId: "q1", decision: "Approved" }).catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiError);
    expect(approvalConflictKind(error)).toBe("room-conflict");

    const conflict = (type?: string) => new ApiError(409, { type, status: 409 });
    expect(approvalConflictKind(conflict("https://studyhive.dev/errors/already-decided"))).toBe("already-decided");
    expect(approvalConflictKind(conflict("https://studyhive.dev/errors/insufficient-stock"))).toBe("insufficient-stock");
    expect(approvalConflictKind(conflict("https://studyhive.dev/errors/proposal-incomplete"))).toBe("proposal-incomplete");
    expect(approvalConflictKind(conflict(undefined))).toBe("conflict");
    expect(approvalConflictKind(new ApiError(400, { status: 400 }))).toBeNull();
    expect(approvalConflictKind(new Error("network"))).toBeNull();
  });

  it("uses the quotation list and detail endpoints", async () => {
    const fetchMock = mockResponse(emptyPage);
    await listQuotations(token, { status: "Proposed", bookingRequestId: "r1" });
    expect(fetchMock.mock.calls[0][0]).toBe("http://localhost:5299/api/quotations?status=Proposed&bookingRequestId=r1");
    await getQuotation(token, "q1");
    expect(fetchMock.mock.calls[1][0]).toBe("http://localhost:5299/api/quotations/q1");
  });

  it("reads workflow executions with their plan and steps", async () => {
    const fetchMock = mockResponse(emptyPage);
    await listWorkflowExecutions(token, { status: "Failed", errorCode: "VALIDATION_FAILED" });
    expect(fetchMock.mock.calls[0][0]).toBe(
      "http://localhost:5299/api/workflow-executions?status=Failed&errorCode=VALIDATION_FAILED",
    );

    mockResponse({
      execution: { id: "w1", status: "Failed", errorCode: "VALIDATION_FAILED", errorMessage: "Over budget by 20.00" },
      plan: { steps: [] },
      steps: [{ id: "s1", stepNumber: 4, attempt: 1, agentName: "Validation", input: { a: 1 }, output: "not json" }],
    });
    const detail = await getWorkflowExecution(token, "w1");
    expect(detail.execution.errorMessage).toBe("Over budget by 20.00");
    expect(detail.steps[0].input).toEqual({ a: 1 });
    expect(detail.steps[0].output).toBe("not json");
  });

  it("sends audit log filters and the bookings report range", async () => {
    const fetchMock = mockResponse(emptyPage);
    await listAuditLogs(token, {
      action: "QuotationApproved", entityType: "Quotation", userId: "u1",
      from: "2026-09-01T00:00:00.000Z", to: "2026-10-01T00:00:00.000Z",
    });
    const url = String(fetchMock.mock.calls[0][0]);
    expect(url).toContain("/api/audit-logs?");
    expect(url).toContain("action=QuotationApproved");
    expect(url).toContain("entityType=Quotation");
    expect(url).toContain("userId=u1");
    expect(url).toContain("from=2026-09-01T00%3A00%3A00.000Z");

    await getBookingsReport(token);
    expect(fetchMock.mock.calls[1][0]).toBe("http://localhost:5299/api/reports/bookings");
    await getBookingsReport(token, "2026-09-01", "2026-10-01");
    expect(fetchMock.mock.calls[2][0]).toBe("http://localhost:5299/api/reports/bookings?from=2026-09-01&to=2026-10-01");
  });
});
