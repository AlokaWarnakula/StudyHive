import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../api/client";
import {
  createMaintenanceWindow, createRoom, getRoom, getRoomSchedule, listEquipment, listMaintenanceWindows, listRooms,
} from "../api/rooms";
import { listStockReservations } from "../api/consumables";
import type { StaffRole } from "../store/authStore";
import { RoomsPage } from "./rooms/RoomsPage";
import { RoomDetailPage } from "./rooms/RoomDetailPage";
import { MaintenancePage } from "./rooms/MaintenancePage";
import { ReservationsPage } from "./store/ReservationsPage";

/** PLAN.md B3: CW-01 dialog errors, CW-03 Sign out, CW-06 overlap confirm, CW-07 columns, W-13 QR. */

vi.mock("../api/rooms", () => ({
  createRoom: vi.fn(),
  listEquipment: vi.fn(),
  listRooms: vi.fn(),
  getRoom: vi.fn(),
  getRoomSchedule: vi.fn(),
  updateRoom: vi.fn(),
  assignRoomEquipment: vi.fn(),
  removeRoomEquipment: vi.fn(),
  listMaintenanceWindows: vi.fn(),
  createMaintenanceWindow: vi.fn(),
  updateMaintenanceWindow: vi.fn(),
  deleteMaintenanceWindow: vi.fn(),
}));

vi.mock("../api/consumables", () => ({
  listStockReservations: vi.fn(),
  releaseStockReservation: vi.fn(),
  markStockReservationUsed: vi.fn(),
}));

vi.mock("qrcode", () => ({ default: { toDataURL: vi.fn().mockResolvedValue("data:image/png;base64,QR") } }));

const auth = vi.hoisted(() => ({ role: "Librarian" as StaffRole }));
vi.mock("../store/authStore", () => ({
  useAuthStore: (selector: (state: unknown) => unknown) =>
    selector({
      accessToken: "test-token",
      user: { id: "u1", name: "Test Staff", email: "staff@studyhive.test", role: auth.role },
      logout: () => undefined,
    }),
}));

const page = <T,>(items: T[]) => ({ items, page: 1, pageSize: 20, totalItems: items.length, totalPages: items.length ? 1 : 0 });

const room = {
  id: "room-1", name: "Quiet Study 101", building: "Main", floor: 1, capacity: 4, hourlyRate: 0,
  qrCode: "STUDYHIVE-QUIET-101", isActive: true, createdAt: "2026-09-01T00:00:00Z", updatedAt: "2026-09-01T00:00:00Z",
  equipment: [],
};

describe("B3 web must-fix", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    auth.role = "Librarian";
    vi.mocked(listRooms).mockResolvedValue(page([room]));
    vi.mocked(listEquipment).mockResolvedValue({ ...page([]), pageSize: 100 });
  });

  it("shows a save error inside the Add room dialog, not behind it (CW-01)", async () => {
    vi.mocked(createRoom).mockRejectedValue(
      new ApiError(400, { title: "One or more validation errors occurred.", errors: { QrCode: ["A room with this QR code already exists."] } }),
    );
    render(<MemoryRouter><RoomsPage /></MemoryRouter>);

    fireEvent.click(await screen.findByRole("button", { name: "Add room" }));
    const dialog = screen.getByRole("dialog");
    for (const [label, value] of [["Room name", "Room X"], ["Building", "Main"], ["Seats", "4"], ["QR code", "STUDYHIVE-QUIET-101"]]) {
      fireEvent.change(within(dialog).getByLabelText(label), { target: { value } });
    }
    fireEvent.click(within(dialog).getByRole("button", { name: "Save room" }));

    expect(await within(dialog).findByRole("alert")).toHaveTextContent("A room with this QR code already exists.");
  });

  it("hides Add room from a role the API refuses (CW-05)", async () => {
    auth.role = "StoreOfficer";
    render(<MemoryRouter><RoomsPage /></MemoryRouter>);
    await screen.findByText("Quiet Study 101");
    expect(screen.queryByRole("button", { name: "Add room" })).toBeNull();
  });

  it("shows the signed-in user and Sign out on a page that used to hide them (CW-03)", async () => {
    render(<MemoryRouter><RoomsPage /></MemoryRouter>);
    await screen.findByText("Quiet Study 101");
    expect(screen.getByRole("button", { name: "Sign out" })).toBeInTheDocument();
    expect(screen.getByText("Test Staff")).toBeInTheDocument();
  });

  it("draws the room's QR code with a print button (W-13)", async () => {
    vi.mocked(getRoom).mockResolvedValue(room);
    vi.mocked(getRoomSchedule).mockResolvedValue([]);
    render(
      <MemoryRouter initialEntries={["/rooms/room-1"]}>
        <Routes><Route path="/rooms/:id" element={<RoomDetailPage />} /></Routes>
      </MemoryRouter>,
    );

    const img = await screen.findByAltText("Check-in QR code for Quiet Study 101");
    expect(img).toHaveAttribute("src", "data:image/png;base64,QR");
    expect(screen.getByRole("button", { name: "Print QR sticker" })).toBeEnabled();
  });

  it("lists the bookings a window overlaps and schedules anyway with force on confirm (CW-06)", async () => {
    vi.mocked(listMaintenanceWindows).mockResolvedValue(page([]));
    vi.mocked(createMaintenanceWindow)
      .mockRejectedValueOnce(new ApiError(409, {
        type: "https://studyhive.dev/errors/maintenance-overlaps-bookings",
        title: "Maintenance overlaps confirmed bookings",
        bookings: [{ bookingId: "b1", bookingRequestId: "r1", studentName: "Nimal Perera", startsAt: "2030-10-06T08:30:00Z", endsAt: "2030-10-06T10:30:00Z" }],
      } as never))
      .mockResolvedValueOnce({ id: "w1", roomId: "room-1", roomName: "Quiet Study 101", startsAt: "", endsAt: "", reason: "AC", affectedBookings: 1, createdAt: "" });
    render(<MemoryRouter><MaintenancePage /></MemoryRouter>);

    await screen.findByRole("option", { name: /Quiet Study 101/ });
    fireEvent.change(screen.getByLabelText("Reason"), { target: { value: "AC" } });
    fireEvent.change(screen.getByLabelText("From"), { target: { value: "2030-10-06T13:00" } });
    fireEvent.change(screen.getByLabelText("To"), { target: { value: "2030-10-06T17:00" } });
    fireEvent.click(screen.getByRole("button", { name: "Save window" }));

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("Nimal Perera")).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole("button", { name: "Schedule anyway and email the students" }));

    await waitFor(() => expect(createMaintenanceWindow).toHaveBeenLastCalledWith("test-token", expect.objectContaining({ reason: "AC" }), true));
    expect(await screen.findByRole("status")).toHaveTextContent("the students are emailed");
  });

  it("hides the scheduling form from an Admin, who may only read (CW-05)", async () => {
    auth.role = "Admin";
    vi.mocked(listMaintenanceWindows).mockResolvedValue(page([]));
    render(<MemoryRouter><MaintenancePage /></MemoryRouter>);
    await screen.findByText("No maintenance windows match this search.");
    expect(screen.queryByRole("button", { name: "Save window" })).toBeNull();
  });

  it("shows who, which request, room and booking for each reservation, and filters Due today (CW-07)", async () => {
    auth.role = "StoreOfficer";
    vi.mocked(listStockReservations).mockResolvedValue(page([{
      id: "res-1", bookingRequestItemId: "item-1", consumableId: "c1", consumableName: "Whiteboard markers", quantity: 2,
      status: "Reserved" as const, reservedAt: "2030-10-01T04:00:00Z", releasedAt: null, usedAt: null, createdAt: "2030-10-01T04:00:00Z",
      bookingRequestId: "req-1", requestObjective: "Database revision", studentName: "Nimal Perera",
      roomName: "Quiet Study 101", slotStartsAt: "2030-10-06T08:30:00Z", slotEndsAt: "2030-10-06T10:30:00Z",
    }]));
    render(<MemoryRouter><ReservationsPage /></MemoryRouter>);

    expect(await screen.findByText("Nimal Perera")).toBeInTheDocument();
    expect(screen.getByText("Database revision")).toBeInTheDocument();
    expect(screen.getByText("Quiet Study 101")).toBeInTheDocument();
    expect(screen.getByText("Sun 6 Oct · 14:00–16:00")).toBeInTheDocument();

    fireEvent.click(screen.getByLabelText("Due today"));
    await waitFor(() => expect(listStockReservations).toHaveBeenLastCalledWith("test-token", expect.objectContaining({ dueOn: expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/) })));
  });
});
