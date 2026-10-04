import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  getApproval,
  getBookingsReport,
  getWorkflowExecution,
  listApprovals,
  listWorkflowExecutions,
  submitApprovalDecision,
  type ApprovalDetail,
  type ApprovalQueueItem,
  type BookingsReport,
  type WorkflowExecutionDetail,
} from "../../api/approvals";
import { getBookingRequest, type BookingRequest } from "../../api/bookingRequests";
import { ApiError } from "../../api/client";
import type { StaffRole } from "../../store/authStore";
import { DashboardPage } from "../DashboardPage";
import { ReportsPage } from "../reports/ReportsPage";
import { ApprovalQueuePage } from "./ApprovalQueuePage";
import { ReviewProposalPage } from "./ReviewProposalPage";
import { WorkflowExecutionPage } from "./WorkflowExecutionPage";

vi.mock("../../api/approvals", async (importOriginal) => ({
  // approvalConflictKind stays real: the 409 mapping is part of what these tests check.
  ...(await importOriginal<typeof import("../../api/approvals")>()),
  getApproval: vi.fn(),
  getBookingsReport: vi.fn(),
  getWorkflowExecution: vi.fn(),
  listApprovals: vi.fn(),
  listAuditLogs: vi.fn(),
  listWorkflowExecutions: vi.fn(),
  submitApprovalDecision: vi.fn(),
}));

vi.mock("../../api/bookingRequests", () => ({ getBookingRequest: vi.fn() }));
vi.mock("../../api/consumables", () => ({ listLowStock: vi.fn() }));

const auth = vi.hoisted(() => ({ role: "Librarian" as StaffRole }));
vi.mock("../../store/authStore", () => ({
  useAuthStore: (selector: (state: unknown) => unknown) =>
    selector({
      accessToken: "test-token",
      user: { id: "u1", name: "Test", email: "t@studyhive.test", role: auth.role },
      logout: () => undefined,
    }),
}));

function page<T>(items: T[]) {
  return { items, page: 1, pageSize: 20, totalItems: items.length, totalPages: items.length ? 1 : 0 };
}

const pending: ApprovalQueueItem = {
  quotationId: "q-1",
  bookingRequestId: "r-1",
  studentId: "s-1",
  objective: "Revise for the networks exam",
  groupSize: 4,
  version: 1,
  roomFee: 0,
  consumableCost: 120,
  totalAmount: 120,
  budgetSnapshot: 500,
  withinBudget: true,
  currency: "LKR",
  quotationStatus: "Proposed",
  createdAt: "2026-09-30T03:00:00Z",
  decision: null,
  status: "Pending",
};

const detail: ApprovalDetail = {
  item: pending,
  lineItems: [
    { id: "l-1", itemType: "Room", itemName: "Quiet Study 101", roomId: "room-1", roomBookingId: null, consumableId: null, quantity: 2, unitPrice: 0, lineTotal: 0 },
    { id: "l-2", itemType: "Consumable", itemName: "Whiteboard markers", roomId: null, roomBookingId: null, consumableId: "c-1", quantity: 2, unitPrice: 60, lineTotal: 120 },
  ],
};

const request = {
  id: "r-1",
  preferredDateFrom: "2026-10-05",
  preferredDateTo: "2026-10-05",
  preferredTimeFrom: "09:00:00",
  preferredTimeTo: "12:00:00",
  sessionsRequired: 1,
  sessionDurationMinutes: 120,
  notes: null,
  latestWorkflowId: "w-1",
} as BookingRequest;

const workflow: WorkflowExecutionDetail = {
  execution: {
    id: "w-1", bookingRequestId: "r-1", objective: pending.objective, status: "PendingApproval", currentStep: 4, totalSteps: 4,
    errorCode: null, errorMessage: null, startedAt: "2026-09-30T02:59:00Z", completedAt: null, updatedAt: "2026-09-30T03:00:00Z",
  },
  plan: { steps: ["schedule", "resource", "validate"] },
  steps: [
    {
      id: "st-4", stepNumber: 4, attempt: 1, agentName: "Validation", toolName: "validate_proposal",
      input: { proposedSlots: [{ roomId: "room-1", roomName: "Quiet Study 101", startsAt: "2026-10-05T03:30:00Z", endsAt: "2026-10-05T05:30:00Z" }] },
      output: { valid: true, results: [{ rule: "validate_no_overlap", passed: true, detail: "No clashes" }] },
      validationResult: "Pass", validationDetails: null, errorMessage: null, durationMs: 12, createdAt: "2026-09-30T03:00:00Z",
    },
  ],
};

function renderReview() {
  return render(
    <MemoryRouter initialEntries={["/approvals/q-1"]}>
      <Routes>
        <Route path="/approvals/:id" element={<ReviewProposalPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  auth.role = "Librarian";
  vi.mocked(getApproval).mockResolvedValue(detail);
  vi.mocked(getBookingRequest).mockResolvedValue(request);
  vi.mocked(getWorkflowExecution).mockResolvedValue(workflow);
});

describe("W-03 approval queue", () => {
  it("lists the live pending queue and opens a quotation for review", async () => {
    vi.mocked(listApprovals).mockResolvedValue(page([pending]));
    render(
      <MemoryRouter initialEntries={["/approvals"]}>
        <Routes>
          <Route path="/approvals" element={<ApprovalQueuePage />} />
          <Route path="/approvals/:id" element={<p>review {"q-1"}</p>} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByText("Revise for the networks exam")).toBeInTheDocument();
    expect(listApprovals).toHaveBeenCalledWith("test-token", expect.objectContaining({ status: "Pending", sortBy: "createdAt", sortDir: "asc" }));
    expect(screen.getAllByText("Rs. 120.00")).toHaveLength(2); // items and total

    fireEvent.change(screen.getByRole("combobox", { name: "Status" }), { target: { value: "Rejected" } });
    await waitFor(() => expect(listApprovals).toHaveBeenLastCalledWith("test-token", expect.objectContaining({ status: "Rejected" })));

    fireEvent.click(screen.getByRole("button", { name: "Review" }));
    expect(await screen.findByText("review q-1")).toBeInTheDocument();
  });

  it("shows the empty and error states", async () => {
    vi.mocked(listApprovals).mockResolvedValueOnce(page([]));
    const { unmount } = render(<MemoryRouter><ApprovalQueuePage /></MemoryRouter>);
    expect(await screen.findByText("Nothing here — the queue is clear.")).toBeInTheDocument();
    unmount();

    vi.mocked(listApprovals).mockRejectedValueOnce(new ApiError(500, { title: "Server error" }));
    render(<MemoryRouter><ApprovalQueuePage /></MemoryRouter>);
    expect(await screen.findByRole("alert")).toHaveTextContent("Server error");
  });
});

describe("W-04 review proposal", () => {
  it("shows the proposed slot, the validation checks and the quotation lines", async () => {
    renderReview();
    expect(await screen.findByRole("heading", { name: "Revise for the networks exam" })).toBeInTheDocument();
    expect(await screen.findByText("No overlap")).toBeInTheDocument();
    expect(screen.getByText("No clashes")).toBeInTheDocument();
    expect(screen.getAllByText("Quiet Study 101").length).toBeGreaterThanOrEqual(2); // slot + quotation line
    expect(screen.getByText("2 h")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "See workflow steps" })).toHaveAttribute("href", "/workflows/w-1");
  });

  it("requires a comment to reject, then sends { quotationId, decision, comments }", async () => {
    vi.mocked(submitApprovalDecision).mockResolvedValue({
      id: "d-1", quotationId: "q-1", decidedBy: "u1", decidedByRole: "Librarian", decision: "Rejected", comments: "Too big", decidedAt: "2026-09-30T04:00:00Z",
    });
    renderReview();
    fireEvent.click(await screen.findByRole("button", { name: "Reject" }));
    expect(screen.getByRole("alert")).toHaveTextContent("Write a comment");
    expect(submitApprovalDecision).not.toHaveBeenCalled();

    fireEvent.change(screen.getByRole("textbox", { name: "Comment to the student" }), { target: { value: "  Too big  " } });
    fireEvent.click(screen.getByRole("button", { name: "Reject" }));
    await waitFor(() =>
      expect(submitApprovalDecision).toHaveBeenCalledWith("test-token", { quotationId: "q-1", decision: "Rejected", comments: "Too big" }),
    );
    await waitFor(() => expect(getApproval).toHaveBeenCalledTimes(2)); // reloaded to show the decision
  });

  it("explains a 409 room conflict on approval", async () => {
    vi.mocked(submitApprovalDecision).mockRejectedValue(
      new ApiError(409, {
        type: "https://studyhive.dev/errors/room-conflict",
        title: "Room can no longer be booked",
        status: 409,
        detail: "Quiet Study 101 is booked 09:00–11:00.",
      }),
    );
    renderReview();
    fireEvent.click(await screen.findByRole("button", { name: "Approve booking" }));
    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("The room is no longer free for this slot. Nothing was booked or reserved.");
    expect(alert).toHaveTextContent("Quiet Study 101 is booked 09:00–11:00.");
    expect(submitApprovalDecision).toHaveBeenCalledWith("test-token", { quotationId: "q-1", decision: "Approved", comments: null });
  });

  it("shows the standing decision instead of the form once decided", async () => {
    vi.mocked(getApproval).mockResolvedValue({
      ...detail,
      item: {
        ...pending, status: "RevisionRequested", quotationStatus: "Rejected",
        decision: { id: "d-1", quotationId: "q-1", decidedBy: "u1", decidedByRole: "Librarian", decision: "RevisionRequested", comments: "Pick an afternoon", decidedAt: "2026-09-30T04:00:00Z" },
      },
    });
    renderReview();
    expect(await screen.findByText("“Pick an afternoon”")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Approve booking" })).not.toBeInTheDocument();
  });
});

describe("W-06 workflow execution", () => {
  it("renders step payloads as text, never as markup", async () => {
    vi.mocked(getWorkflowExecution).mockResolvedValue({
      ...workflow,
      steps: [{ ...workflow.steps[0], output: "<img src=x onerror=alert(1)>" }],
    });
    const { container } = render(
      <MemoryRouter initialEntries={["/workflows/w-1"]}>
        <Routes>
          <Route path="/workflows/:id" element={<WorkflowExecutionPage />} />
        </Routes>
      </MemoryRouter>,
    );
    expect(await screen.findByText("<img src=x onerror=alert(1)>")).toBeInTheDocument();
    expect(container.querySelector("img")).toBeNull();
    expect(screen.getByText(/"roomName": "Quiet Study 101"/)).toBeInTheDocument();
  });
});

describe("Dashboard and reports", () => {
  const report: BookingsReport = {
    from: "2026-09-01T00:00:00Z",
    to: "2026-10-01T00:00:00Z",
    totalRequests: 7,
    byStatus: [{ status: "Approved", count: 3 }, { status: "PendingApproval", count: 4 }],
    byWeek: [{ weekStart: "2026-09-28", requests: 7, approved: 3, approvedSpend: 360, approvedBudget: 1500 }],
    spend: { approvedQuotations: 3, totalSpend: 360, totalBudget: 1500, withinBudget: 3, overBudget: 0 },
  };

  it("gives a librarian live queue and failed-run counts", async () => {
    vi.mocked(listApprovals).mockResolvedValue({ ...page([pending]), totalItems: 6 });
    vi.mocked(listWorkflowExecutions).mockResolvedValue({ ...page([]), totalItems: 2 });
    vi.mocked(getBookingsReport).mockResolvedValue(report);
    render(<MemoryRouter><DashboardPage /></MemoryRouter>);

    const waiting = (await screen.findByText("Waiting for approval")).closest(".tile") as HTMLElement;
    expect(within(waiting).getByText("6")).toBeInTheDocument();
    const failed = screen.getByText("Failed workflow runs").closest(".tile") as HTMLElement;
    expect(within(failed).getByText("2")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Revise for the networks exam" })).toHaveAttribute("href", "/approvals/q-1");
  });

  it("does not call Librarian-only endpoints for an admin, nor offer the room-use tab", async () => {
    auth.role = "Admin";
    vi.mocked(getBookingsReport).mockResolvedValue(report);
    render(<MemoryRouter><ReportsPage /></MemoryRouter>);

    expect(await screen.findByText("Approved quotations")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Bookings by status" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Room use" })).not.toBeInTheDocument();
    expect(listApprovals).not.toHaveBeenCalled();
  });
});
