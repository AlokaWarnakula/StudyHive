import { afterEach, describe, expect, it, vi } from "vitest";
import {
  createMaintenanceWindow,
  createRoom,
  getRoomSchedule,
  listEquipment,
  listRooms,
  updateEquipmentType,
} from "./rooms";

const token = "test-token";

afterEach(() => vi.unstubAllGlobals());

function mockJson(body: unknown = {}) {
  const fetchMock = vi.fn().mockImplementation(async () => new Response(JSON.stringify(body), {
      status: 200,
      headers: { "Content-Type": "application/json" },
    }));
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

describe("S2 rooms API client", () => {
  it("sends room list filters using the shared paging contract", async () => {
    const fetchMock = mockJson({ items: [], page: 2, pageSize: 20, totalItems: 0, totalPages: 0 });
    await listRooms(token, { page: 2, pageSize: 20, search: "library", capacity: 8, equipmentTypeId: "projector-id", sortBy: "name", sortDir: "asc" });
    expect(fetchMock.mock.calls[0][0]).toBe("http://localhost:5299/api/rooms?page=2&pageSize=20&search=library&capacity=8&equipmentTypeId=projector-id&sortBy=name&sortDir=asc");
    expect(fetchMock.mock.calls[0][1].headers.Authorization).toBe(`Bearer ${token}`);
  });

  it("posts the exact room body", async () => {
    const fetchMock = mockJson();
    const body = { name: "A-101", building: "Main", floor: 1, capacity: 6, hourlyRate: 100, qrCode: "A-101-QR" };
    await createRoom(token, body);
    expect(fetchMock.mock.calls[0][1]).toMatchObject({ method: "POST", body: JSON.stringify(body) });
  });

  it("requests one room schedule with its ISO range", async () => {
    const fetchMock = mockJson([]);
    await getRoomSchedule(token, "room-id", "2026-09-14T00:00:00.000Z", "2026-09-21T00:00:00.000Z");
    const url = String(fetchMock.mock.calls[0][0]);
    expect(url).toContain("/api/rooms/room-id/schedule?");
    expect(url).toContain("from=2026-09-14T00%3A00%3A00.000Z");
    expect(url).toContain("to=2026-09-21T00%3A00%3A00.000Z");
  });

  it("uses the live equipment catalogue and update endpoints", async () => {
    const fetchMock = mockJson({ items: [], page: 1, pageSize: 100, totalItems: 0, totalPages: 0 });
    await listEquipment(token, { pageSize: 100, sortBy: "name", sortDir: "asc" });
    expect(fetchMock.mock.calls[0][0]).toContain("/api/equipment?pageSize=100&sortBy=name&sortDir=asc");
    await updateEquipmentType(token, "equipment-id", { name: "Projector", category: "Display", description: null, isActive: true });
    expect(fetchMock.mock.calls[1][0]).toBe("http://localhost:5299/api/equipment/equipment-id");
    expect(fetchMock.mock.calls[1][1].method).toBe("PUT");
  });

  it("posts a maintenance window with API field names", async () => {
    const fetchMock = mockJson();
    const body = { roomId: "room-id", startsAt: "2026-09-14T04:00:00.000Z", endsAt: "2026-09-14T06:00:00.000Z", reason: "Repair" };
    await createMaintenanceWindow(token, body);
    expect(fetchMock.mock.calls[0][0]).toBe("http://localhost:5299/api/maintenance-windows");
    expect(JSON.parse(fetchMock.mock.calls[0][1].body)).toEqual(body);
  });
});
