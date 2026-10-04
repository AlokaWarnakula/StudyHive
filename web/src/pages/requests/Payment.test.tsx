import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../../api/client";
import { getBookingRequest, getWorkflowStatus, listBookingRequests, recordPayment } from "../../api/bookingRequests";
import { RequestDetailPage } from "./RequestDetailPage";
import { RequestsPage } from "./RequestsPage";

/** PLAN.md 3.3: "Mark as paid" on request detail (Librarian only) and the Payment column. */

vi.mock("../../api/bookingRequests", async (importOriginal) => ({
  paymentState: (await importOriginal<typeof import("../../api/bookingRequests")>()).paymentState,
  getBookingRequest: vi.fn(),
  getWorkflowStatus: vi.fn(),
  listBookingRequests: vi.fn(),
  recordPayment: vi.fn(),
}));

vi.mock("../../api/consumables", () => ({ getConsumable: vi.fn() }));

const auth = vi.hoisted(() => ({ role: "Librarian" }));
vi.mock("../../store/authStore", () => ({
  useAuthStore: (selector: (state: unknown) => unknown) =>
    selector({
      accessToken: "test-token",
      user: { id: "u1", name: "Test Staff", email: "staff@studyhive.test", role: auth.role },
      logout: () => undefined,
    }),
}));

const quotation = {
  id: "q-1",
  status: "Approved",
  version: 1,
  totalAmount: 450,
  currency: "LKR",
  budgetSnapshot: 1000,
  withinBudget: true,
  paidAt: null,
  paymentReference: null,
};

function approvedRequest(overrides: Record<string, unknown> = {}) {
  return {
    id: "req-1",
    studentId: "s1",
    objective: "Group revision",
    groupSize: 3,
    preferredDateFrom: "2026-10-13",
    preferredDateTo: "2026-10-13",
    preferredTimeFrom: "10:00:00",
    preferredTimeTo: "12:00:00",
    sessionsRequired: 1,
    sessionDurationMinutes: 120,
    budget: 1000,
    notes: null,
    status: "Approved",
    items: [],
    latestWorkflowId: null,
    roomBookings: [],
    latestQuotation: quotation,
    createdAt: "2026-10-04T03:00:00Z",
    updatedAt: "2026-10-04T05:00:00Z",
    ...overrides,
  };
}

function renderDetail() {
  return render(
    <MemoryRouter initialEntries={["/requests/req-1"]}>
      <Routes>
        <Route path="/requests/:id" element={<RequestDetailPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("PLAN.md 3.3 payment", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    auth.role = "Librarian";
    vi.mocked(getWorkflowStatus).mockRejectedValue(new ApiError(404, { title: "Not Found", status: 404 }));
  });

  it("Librarian marks an unpaid approved request as paid with a receipt number", async () => {
    vi.mocked(getBookingRequest).mockResolvedValue(approvedRequest() as never);
    vi.mocked(recordPayment).mockResolvedValue({ ...quotation, paidAt: "2026-10-05T04:00:00Z", paymentReference: "RCPT-7" } as never);
    renderDetail();

    expect(await screen.findByText("Unpaid")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Mark as paid" }));
    fireEvent.change(screen.getByLabelText("Receipt number (optional)"), { target: { value: "RCPT-7" } });
    fireEvent.click(screen.getByRole("button", { name: "Save payment" }));

    await waitFor(() => expect(recordPayment).toHaveBeenCalledWith("test-token", "req-1", "RCPT-7"));
    expect(await screen.findByText(/receipt RCPT-7/)).toBeInTheDocument();
    expect(screen.getByText("Paid")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Mark as paid" })).not.toBeInTheDocument();
  });

  it("shows a conflict from the API inside the dialog", async () => {
    vi.mocked(getBookingRequest).mockResolvedValue(approvedRequest() as never);
    vi.mocked(recordPayment).mockRejectedValue(new ApiError(409, { title: "Already paid", status: 409 }));
    renderDetail();

    fireEvent.click(await screen.findByRole("button", { name: "Mark as paid" }));
    fireEvent.click(screen.getByRole("button", { name: "Save payment" }));
    expect(await screen.findByRole("alert")).toBeInTheDocument();
  });

  it("an already paid request shows the date and receipt, no button", async () => {
    vi.mocked(getBookingRequest).mockResolvedValue(
      approvedRequest({ latestQuotation: { ...quotation, paidAt: "2026-10-05T04:00:00Z", paymentReference: "R-1" } }) as never,
    );
    renderDetail();

    expect(await screen.findByText(/receipt R-1/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Mark as paid" })).not.toBeInTheDocument();
  });

  it.each(["StoreOfficer", "Admin"])("%s sees the payment state but no Mark as paid button", async (role) => {
    auth.role = role;
    vi.mocked(getBookingRequest).mockResolvedValue(approvedRequest() as never);
    renderDetail();

    expect(await screen.findByText("Unpaid")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Mark as paid" })).not.toBeInTheDocument();
  });

  it("a cancelled booking with an Approved quotation has no payment tile", async () => {
    vi.mocked(getBookingRequest).mockResolvedValue(approvedRequest({ status: "Cancelled" }) as never);
    renderDetail();

    expect(await screen.findByText("Group revision", { exact: false })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Mark as paid" })).not.toBeInTheDocument();
    expect(screen.queryByText("Unpaid")).not.toBeInTheDocument();
  });

  it("a request that is not approved has no payment tile", async () => {
    vi.mocked(getBookingRequest).mockResolvedValue(
      approvedRequest({ status: "PendingApproval", latestQuotation: { ...quotation, status: "Proposed" } }) as never,
    );
    renderDetail();

    expect(await screen.findByText("Group revision", { exact: false })).toBeInTheDocument();
    expect(screen.queryByText("Payment")).not.toBeInTheDocument();
  });

  it("the requests list shows Paid / Unpaid / — in the Payment column", async () => {
    vi.mocked(listBookingRequests).mockResolvedValue({
      items: [
        approvedRequest({ id: "aaaaaaaa-1", latestQuotation: { ...quotation, paidAt: "2026-10-05T04:00:00Z" } }),
        approvedRequest({ id: "bbbbbbbb-2" }),
        approvedRequest({ id: "cccccccc-3", status: "Draft", latestQuotation: null }),
      ],
      page: 1,
      pageSize: 20,
      totalItems: 3,
      totalPages: 1,
    } as never);
    render(
      <MemoryRouter>
        <RequestsPage />
      </MemoryRouter>,
    );

    expect(await screen.findByText("Paid")).toBeInTheDocument();
    expect(screen.getByText("Unpaid")).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Payment" })).toBeInTheDocument();
  });
});
