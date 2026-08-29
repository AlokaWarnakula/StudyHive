/**
 * S2 — Rooms & Availability API client. SCAFFOLD.
 *
 * The functions are written and typed; the endpoints behind them return 501 until S2 implements
 * `api/src/StudyHive.Api/Controllers/Rooms/`. Calling one today throws an `ApiError` with status
 * 501, which is the honest answer — nothing here fabricates data.
 *
 * Screens this backs: W-13 Rooms, W-14 Room detail, W-15 Room calendar, W-16 Equipment,
 * W-17 Maintenance windows; mobile M-09 Browse rooms, M-10 Room detail, M-11 Free times.
 *
 * S2, to switch a screen on: the page currently calls `useFixture`. Replace that with `useState` +
 * `useEffect` calling the function below. `web/src/pages/requests/RequestsPage.tsx` is the working
 * reference — it already solves search, filter, sort, pagination and the empty state against the
 * real API. Do not change these signatures without updating the pages that call them.
 *
 * The interfaces below are the contract from the plan's §10 schema. Adjust them to match what you
 * actually return, then update the pages — but keep the shapes camelCase, and keep every list
 * returning `PagedResult<T>`.
 */

import { apiFetch } from "./client";
import type { PagedResult } from "./bookingRequests";

export type { PagedResult } from "./bookingRequests";

export interface Room {
  id: string;
  name: string;
  building: string;
  floor: number;
  capacity: number;
  hourlyRate: number;
  qrCode: string;
  isActive: boolean;
}

export interface RoomEquipmentLine {
  equipmentTypeId: string;
  name: string;
  quantity: number;
}

export interface RoomDetail extends Room {
  equipment: RoomEquipmentLine[];
}

export interface EquipmentType {
  id: string;
  name: string;
  category: string;
  description: string | null;
  isActive: boolean;
}

export interface MaintenanceWindow {
  id: string;
  roomId: string;
  startsAt: string;
  endsAt: string;
  reason: string;
}

/**
 * One block in the W-15 week grid / M-11 free-times list.
 *
 * A DERIVED view model, not a table row: the endpoint merges `room_bookings` and
 * `maintenance_windows` into one timeline. `kind` is yours to define — it is not a status column,
 * so it does not have to match `room_bookings.status` (Confirmed / Cancelled / Completed / NoShow).
 */
export interface ScheduleSlot {
  roomId: string;
  roomName: string;
  startsAt: string;
  endsAt: string;
  kind: "Booked" | "Held" | "Maintenance";
}

export interface ListRoomsParams {
  page?: number;
  pageSize?: number;
  search?: string;
  sortBy?: string;
  sortDir?: "asc" | "desc";
}

/** Criteria for the availability search — the hardest query in the system. */
export interface AvailabilityParams {
  from: string;
  to: string;
  capacity?: number;
  equipmentTypeId?: string;
}

function buildQuery(params: object): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params) as [string, string | number | undefined][]) {
    if (value !== undefined && value !== "") search.set(key, String(value));
  }
  const qs = search.toString();
  return qs ? `?${qs}` : "";
}

// TODO(S2): implement GET /api/rooms
export function listRooms(token: string, params: ListRoomsParams = {}): Promise<PagedResult<Room>> {
  return apiFetch(`/api/rooms${buildQuery(params)}`, { token });
}

// TODO(S2): implement GET /api/rooms/available
export function searchAvailableRooms(token: string, params: AvailabilityParams): Promise<PagedResult<Room>> {
  return apiFetch(`/api/rooms/available${buildQuery(params)}`, { token });
}

// TODO(S2): implement GET /api/rooms/{id}
export function getRoom(token: string, id: string): Promise<RoomDetail> {
  return apiFetch(`/api/rooms/${id}`, { token });
}

// TODO(S2): implement POST /api/rooms
export function createRoom(token: string, body: Omit<Room, "id" | "isActive">): Promise<Room> {
  return apiFetch(`/api/rooms`, { method: "POST", token, body });
}

// TODO(S2): implement PUT /api/rooms/{id}
export function updateRoom(token: string, id: string, body: Omit<Room, "id">): Promise<Room> {
  return apiFetch(`/api/rooms/${id}`, { method: "PUT", token, body });
}

// TODO(S2): implement DELETE /api/rooms/{id} — deactivation, not a physical delete
export function deactivateRoom(token: string, id: string): Promise<void> {
  return apiFetch(`/api/rooms/${id}`, { method: "DELETE", token });
}

// TODO(S2): implement GET /api/rooms/{id}/schedule
export function getRoomSchedule(token: string, id: string, from: string, to: string): Promise<ScheduleSlot[]> {
  return apiFetch(`/api/rooms/${id}/schedule${buildQuery({ from, to })}`, { token });
}

// TODO(S2): implement GET /api/equipment
export function listEquipment(token: string, params: ListRoomsParams = {}): Promise<PagedResult<EquipmentType>> {
  return apiFetch(`/api/equipment${buildQuery(params)}`, { token });
}

// TODO(S2): implement GET /api/maintenance-windows
export function listMaintenanceWindows(token: string, params: ListRoomsParams = {}): Promise<PagedResult<MaintenanceWindow>> {
  return apiFetch(`/api/maintenance-windows${buildQuery(params)}`, { token });
}

// TODO(S2): implement POST /api/maintenance-windows
export function createMaintenanceWindow(
  token: string,
  body: Omit<MaintenanceWindow, "id">,
): Promise<MaintenanceWindow> {
  return apiFetch(`/api/maintenance-windows`, { method: "POST", token, body });
}
