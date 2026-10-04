import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../api/client";
import { getBookingRequest, getWorkflowStatus } from "../api/bookingRequests";
import { getConsumable } from "../api/consumables";
import { getRoom, getRoomSchedule, listEquipment, removeRoomEquipment } from "../api/rooms";
import { formatItemName } from "./approvals/s4";
import { RequestDetailPage } from "./requests/RequestDetailPage";
import { RoomDetailPage } from "./rooms/RoomDetailPage";

/** PLAN.md Phase C web: CW-10 request detail, CW-11 room lines, CW-13 Remove equipment confirm. */

vi.mock("../api/bookingRequests", async (importOriginal) => ({
  paymentState: (await importOriginal<typeof import("../api/bookingRequests")>()).paymentState,
  getBookingRequest: vi.fn(),
  getWorkflowStatus: vi.fn(),
  recordPayment: vi.fn(),
}));

vi.mock("../api/consumables", () => ({ getConsumable: vi.fn() }));

vi.mock("../api/rooms", () => ({
  getRoom: vi.fn(),
  getRoomSchedule: vi.fn(),
  listEquipment: vi.fn(),
  updateRoom: vi.fn(),
  assignRoomEquipment: vi.fn(),
  removeRoomEquipment: vi.fn(),
}));

vi.mock("qrcode", () => ({ default: { toDataURL: vi.fn().mockResolvedValue("data:image/png;base64,QR") } }));

vi.mock("../store/authStore", () => ({
  useAuthStore: (selector: (state: unknown) => unknown) =>
    selector({
      accessToken: "test-token",
      user: { id: "u1", name: "Test Staff", email: "staff@studyhive.test", role: "Librarian" },
      logout: () => undefined,
    }),
}));

const request = {
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
  items: [{ consumableId: "c-1", quantity: 2 }],
  latestWorkflowId: "wf-1",
  roomBookings: [
    { id: "b-1", roomId: "r-1", roomName: "Quiet Study 101", startsAt: "2026-10-13T04:30:00Z", endsAt: "2026-10-13T06:30:00Z", status: "Confirmed", checkedInAt: null },
  ],
  createdAt: "2026-10-04T03:00:00Z",
  updatedAt: "2026-10-04T05:00:00Z",
};

function renderRequest() {
  return render(
    <MemoryRouter initialEntries={["/requests/req-1"]}>
      <Routes>
        <Route path="/requests/:id" element={<RequestDetailPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("C web polish", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(getWorkflowStatus).mockRejectedValue(new ApiError(404, { title: "Not Found", status: 404 }));
  });

  it("CW-10: request items show consumable names, times have no seconds, no stale S3 text", async () => {
    vi.mocked(getBookingRequest).mockResolvedValue(request as never);
    vi.mocked(getConsumable).mockResolvedValue({ consumable: { name: "A4 printouts" }, recentTransactions: [] } as never);
    renderRequest();

    expect(await screen.findByText("A4 printouts")).toBeInTheDocument();
    expect(screen.queryByText("c-1")).not.toBeInTheDocument();
    expect(screen.getByText("10:00 – 12:00")).toBeInTheDocument();
    expect(screen.queryByText(/not built yet/)).not.toBeInTheDocument();
    // The timeline is the request's own history, including its room booking.
    expect(screen.getByText("Request created")).toBeInTheDocument();
    expect(screen.getByText("Room booked · Quiet Study 101 · Tue 13 Oct · 10:00–12:00")).toBeInTheDocument();
  });

  it("CW-10: an unknown request id says so and links back", async () => {
    vi.mocked(getBookingRequest).mockRejectedValue(new ApiError(404, { title: "Not Found", status: 404 }));
    renderRequest();

    expect(await screen.findByRole("heading", { name: "Request not found" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Back to booking requests" })).toHaveAttribute("href", "/requests");
  });

  it("CW-11: quotation room lines read as a Colombo date and time", () => {
    expect(formatItemName("Quiet Study 101 2026-10-13T10:00:00+05:30")).toBe("Quiet Study 101 · Tue 13 Oct, 10:00");
    expect(formatItemName("Quiet Study 101 2026-10-13T04:30:00Z")).toBe("Quiet Study 101 · Tue 13 Oct, 10:00");
    expect(formatItemName("A4 printouts")).toBe("A4 printouts");
  });

  it("CW-13: Remove equipment asks first and only removes on confirm", async () => {
    vi.mocked(getRoom).mockResolvedValue({
      id: "room-1", name: "Quiet Study 101", building: "Main", floor: 1, capacity: 4, hourlyRate: 0,
      qrCode: "STUDYHIVE-QUIET-101", isActive: true, createdAt: "2026-09-01T00:00:00Z", updatedAt: "2026-09-01T00:00:00Z",
      equipment: [{ equipmentTypeId: "eq-1", name: "Projector", quantity: 1 }],
    } as never);
    vi.mocked(getRoomSchedule).mockResolvedValue([]);
    vi.mocked(listEquipment).mockResolvedValue({ items: [], page: 1, pageSize: 100, totalItems: 0, totalPages: 0 } as never);
    vi.mocked(removeRoomEquipment).mockResolvedValue(undefined as never);
    render(
      <MemoryRouter initialEntries={["/rooms/room-1"]}>
        <Routes>
          <Route path="/rooms/:id" element={<RoomDetailPage />} />
        </Routes>
      </MemoryRouter>,
    );

    fireEvent.click(await screen.findByRole("button", { name: "Remove" }));
    const dialog = await screen.findByRole("dialog");
    expect(dialog).toHaveTextContent("Projector");
    expect(removeRoomEquipment).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByRole("button", { name: "Remove" }));
    await waitFor(() => expect(removeRoomEquipment).toHaveBeenCalledWith("test-token", "room-1", "eq-1"));
  });
});
