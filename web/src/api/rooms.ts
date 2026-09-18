/** S2 — typed clients for the live Rooms, Equipment and Maintenance APIs. */

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
  createdAt: string;
  updatedAt: string;
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
  createdAt: string;
  updatedAt: string;
}

export interface MaintenanceWindow {
  id: string;
  roomId: string;
  roomName: string;
  startsAt: string;
  endsAt: string;
  reason: string;
  affectedBookings: number;
  createdAt: string;
}

export interface RoomUsageRow {
  roomId: string;
  roomName: string;
  bookingCount: number;
  bookedHours: number;
  utilisationPercent: number;
  noShows: number;
}

export interface RoomUsageHour {
  hour: number;
  bookingCount: number;
}

export interface RoomUsageReport {
  from: string;
  to: string;
  totalBookings: number;
  totalBookedHours: number;
  averageUtilisationPercent: number;
  noShows: number;
  busiestRoom: string | null;
  byRoom: RoomUsageRow[];
  bookingsByHour: RoomUsageHour[];
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
  capacity?: number;
  equipmentTypeId?: string;
  sortBy?: string;
  sortDir?: "asc" | "desc";
}

export interface RoomInput {
  name: string;
  building: string;
  floor: number;
  capacity: number;
  hourlyRate: number;
  qrCode: string;
}

export interface EquipmentTypeInput {
  name: string;
  category: string;
  description: string | null;
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

export function listRooms(token: string, params: ListRoomsParams = {}): Promise<PagedResult<Room>> {
  return apiFetch(`/api/rooms${buildQuery(params)}`, { token });
}

export function searchAvailableRooms(token: string, params: AvailabilityParams): Promise<PagedResult<Room>> {
  return apiFetch(`/api/rooms/available${buildQuery(params)}`, { token });
}

export function getRoom(token: string, id: string): Promise<RoomDetail> {
  return apiFetch(`/api/rooms/${id}`, { token });
}

export function createRoom(token: string, body: RoomInput): Promise<Room> {
  return apiFetch(`/api/rooms`, { method: "POST", token, body });
}

export function updateRoom(token: string, id: string, body: RoomInput & { isActive: boolean }): Promise<Room> {
  return apiFetch(`/api/rooms/${id}`, { method: "PUT", token, body });
}

export function deactivateRoom(token: string, id: string): Promise<void> {
  return apiFetch(`/api/rooms/${id}`, { method: "DELETE", token });
}

export function getRoomSchedule(token: string, id: string, from: string, to: string): Promise<ScheduleSlot[]> {
  return apiFetch(`/api/rooms/${id}/schedule${buildQuery({ from, to })}`, { token });
}

export function getRoomUsageReport(token: string, from: string, to: string): Promise<RoomUsageReport> {
  return apiFetch(`/api/reports/room-usage${buildQuery({ from, to })}`, { token });
}

export function listEquipment(token: string, params: ListRoomsParams = {}): Promise<PagedResult<EquipmentType>> {
  return apiFetch(`/api/equipment${buildQuery(params)}`, { token });
}

export function listMaintenanceWindows(token: string, params: ListRoomsParams = {}): Promise<PagedResult<MaintenanceWindow>> {
  return apiFetch(`/api/maintenance-windows${buildQuery(params)}`, { token });
}

export function createMaintenanceWindow(
  token: string,
  body: { roomId: string; startsAt: string; endsAt: string; reason: string },
): Promise<MaintenanceWindow> {
  return apiFetch(`/api/maintenance-windows`, { method: "POST", token, body });
}

export function createEquipmentType(token: string, body: EquipmentTypeInput): Promise<EquipmentType> {
  return apiFetch(`/api/equipment`, { method: "POST", token, body });
}

export function updateEquipmentType(
  token: string,
  id: string,
  body: EquipmentTypeInput & { isActive: boolean },
): Promise<EquipmentType> {
  return apiFetch(`/api/equipment/${id}`, { method: "PUT", token, body });
}

export function assignRoomEquipment(
  token: string,
  roomId: string,
  body: { equipmentTypeId: string; quantity: number },
): Promise<RoomEquipmentLine> {
  return apiFetch(`/api/rooms/${roomId}/equipment`, { method: "POST", token, body });
}

export function removeRoomEquipment(token: string, roomId: string, equipmentTypeId: string): Promise<void> {
  return apiFetch(`/api/rooms/${roomId}/equipment/${equipmentTypeId}`, { method: "DELETE", token });
}
