import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { listEquipment, listRooms } from "../../api/rooms";
import { RoomsPage } from "./RoomsPage";

vi.mock("../../api/rooms", () => ({
  createRoom: vi.fn(),
  listEquipment: vi.fn(),
  listRooms: vi.fn(),
}));

vi.mock("../../store/authStore", () => ({
  useAuthStore: (selector: (state: { accessToken: string }) => unknown) =>
    selector({ accessToken: "test-token" }),
}));

const emptyPage = {
  items: [],
  page: 1,
  pageSize: 20,
  totalItems: 0,
  totalPages: 0,
};

describe("S2 Rooms page filters", () => {
  beforeEach(() => {
    vi.mocked(listRooms).mockResolvedValue(emptyPage);
    vi.mocked(listEquipment).mockResolvedValue({
      ...emptyPage,
      pageSize: 100,
      items: [{
        id: "projector-1",
        name: "Projector",
        category: "Display",
        description: null,
        isActive: true,
        createdAt: "2026-09-01T00:00:00Z",
        updatedAt: "2026-09-01T00:00:00Z",
      }],
    });
  });

  it("sends minimum capacity and equipment type to the room list API", async () => {
    render(<MemoryRouter><RoomsPage /></MemoryRouter>);

    await screen.findByRole("option", { name: "Projector" });
    await waitFor(() => expect(listRooms).toHaveBeenCalled());
    vi.mocked(listRooms).mockClear();

    fireEvent.change(screen.getByLabelText("Minimum capacity"), {
      target: { value: "8" },
    });
    await waitFor(() => expect(listRooms).toHaveBeenLastCalledWith(
      "test-token",
      expect.objectContaining({ capacity: 8 }),
    ));

    fireEvent.change(screen.getByLabelText("Equipment type"), {
      target: { value: "projector-1" },
    });
    await waitFor(() => expect(listRooms).toHaveBeenLastCalledWith(
      "test-token",
      expect.objectContaining({ capacity: 8, equipmentTypeId: "projector-1" }),
    ));
  });
});
