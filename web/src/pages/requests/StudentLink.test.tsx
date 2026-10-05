import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../../api/client";
import { getBookingRequest, getWorkflowStatus, listBookingRequests } from "../../api/bookingRequests";
import { getStudentProfile, listStudentProfiles } from "../../api/studentProfiles";
import { RequestDetailPage } from "./RequestDetailPage";
import { RequestsPage } from "./RequestsPage";
import { StudentsPage } from "./StudentsPage";

/** Staff can see who made each booking request and open that student's profile. */

vi.mock("../../api/bookingRequests", async (importOriginal) => ({
  paymentState: (await importOriginal<typeof import("../../api/bookingRequests")>()).paymentState,
  getBookingRequest: vi.fn(),
  getWorkflowStatus: vi.fn(),
  listBookingRequests: vi.fn(),
  recordPayment: vi.fn(),
}));

vi.mock("../../api/studentProfiles", () => ({
  getStudentProfile: vi.fn(),
  listStudentProfiles: vi.fn(),
  updateStudentProfile: vi.fn(),
}));

vi.mock("../../api/consumables", () => ({ getConsumable: vi.fn() }));

vi.mock("../../store/authStore", () => ({
  useAuthStore: (selector: (state: unknown) => unknown) =>
    selector({
      accessToken: "test-token",
      user: { id: "u1", name: "Test Librarian", email: "lib@studyhive.test", role: "Librarian" },
      logout: () => undefined,
    }),
}));

const request = {
  id: "req-1",
  studentId: "sp-1",
  studentName: "Nimal Perera",
  studentNumber: "IT24000001",
  studentEmail: "nimal@studyhive.dev",
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
  status: "PendingApproval",
  items: [],
  latestWorkflowId: null,
  roomBookings: [],
  latestQuotation: null,
  createdAt: "2026-10-04T03:00:00Z",
  updatedAt: "2026-10-04T05:00:00Z",
};

const profile = {
  id: "sp-1",
  userId: "u-9",
  fullName: "Nimal Perera",
  email: "nimal@studyhive.dev",
  studentNumber: "IT24000001",
  department: "Computing",
  yearOfStudy: 3,
  maxBookingsPerWeek: 3,
  penaltyPoints: 0,
  suspendedUntil: null,
  isSuspended: false,
  isActive: true,
  createdAt: "2026-09-01T00:00:00Z",
  updatedAt: "2026-09-01T00:00:00Z",
};

describe("who made the request", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(getWorkflowStatus).mockRejectedValue(new ApiError(404, { title: "Not Found", status: 404 }));
  });

  it("the requests list has a Student column linking to the profile", async () => {
    vi.mocked(listBookingRequests).mockResolvedValue({ items: [request], page: 1, pageSize: 20, totalItems: 1, totalPages: 1 } as never);
    render(
      <MemoryRouter>
        <RequestsPage />
      </MemoryRouter>,
    );

    const link = await screen.findByRole("link", { name: "Nimal Perera" });
    expect(link).toHaveAttribute("href", "/students?id=sp-1");
    expect(screen.getByText("IT24000001")).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Student" })).toBeInTheDocument();
  });

  it("the request detail says who requested it", async () => {
    vi.mocked(getBookingRequest).mockResolvedValue(request as never);
    render(
      <MemoryRouter initialEntries={["/requests/req-1"]}>
        <Routes>
          <Route path="/requests/:id" element={<RequestDetailPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByRole("link", { name: "Nimal Perera" })).toHaveAttribute("href", "/students?id=sp-1");
    expect(screen.getByText(/IT24000001 · nimal@studyhive.dev/)).toBeInTheDocument();
  });

  it("opening /students?id=… shows that student's profile panel", async () => {
    vi.mocked(listStudentProfiles).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 } as never);
    vi.mocked(getStudentProfile).mockResolvedValue(profile as never);
    render(
      <MemoryRouter initialEntries={["/students?id=sp-1"]}>
        <Routes>
          <Route path="/students" element={<StudentsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    await waitFor(() => expect(getStudentProfile).toHaveBeenCalledWith("test-token", "sp-1"));
    expect(await screen.findByText("Student profile")).toBeInTheDocument();
    expect(screen.getByText("Nimal Perera")).toBeInTheDocument();
    expect(screen.getByText("Computing")).toBeInTheDocument();
  });
});
