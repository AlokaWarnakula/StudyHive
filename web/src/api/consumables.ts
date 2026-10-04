/**
 * S3 — Consumables & Stock API client, typed against `api/src/StudyHive.Api/Controllers/Store/`
 * (request/response shapes in `StoreContracts.cs`) and `ReportsController.ConsumableUsage`.
 *
 * Screens this backs: W-19 Consumables, W-20 Consumable detail + stock-in, W-21 Low stock,
 * W-22 Stock reservations, W-23 Suppliers, W-24 Consumable usage report.
 *
 * `createStockReservation` is S3's business operation: the API reserves transactionally and cannot
 * oversell under concurrent callers (chk_never_oversold — proven in StockReservationsControllerTests).
 */

import { apiFetch } from "./client";
import type { PagedResult } from "./bookingRequests";

export type { PagedResult } from "./bookingRequests";

/** Mirrors the `consumables` table. Field names are the real columns — see DATABASE.md. */
export interface Consumable {
  id: string;
  name: string;
  description: string | null;
  unit: string;
  unitPrice: number;
  stockQuantity: number;
  reservedQuantity: number;
  /** READ-ONLY. A generated column: `stock_quantity - reserved_quantity`. Never send it. */
  availableQuantity: number;
  minStockLevel: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

/** Mirrors `stock_transactions`. Append-only ledger. */
export interface StockTransaction {
  id: string;
  consumableId: string;
  /** Matches the CHECK constraint on `transaction_type` exactly. */
  transactionType: "StockIn" | "StockOut" | "Reserve" | "Release" | "Adjust";
  /** CHECK is `<> 0`, not `> 0` — a StockOut is negative. */
  quantity: number;
  balanceAfter: number;
  bookingRequestId: string | null;
  stockReservationId: string | null;
  notes: string | null;
  createdBy: string;
  createdAt: string;
}

/** W-20: the consumable plus its 20 most recent ledger rows (`ConsumableDetailResponse`). */
export interface ConsumableDetail {
  consumable: Consumable;
  recentTransactions: StockTransaction[];
}

export type StockReservationStatus = "Pending" | "Reserved" | "Released" | "Used";

export interface StockReservation {
  id: string;
  bookingRequestItemId: string;
  consumableId: string;
  consumableName: string;
  quantity: number;
  /**
   * These four values are the ones the database will accept — see the CHECK constraint on
   * `stock_reservations.status` in DATABASE.md. The W-22 screen labels the same lifecycle as
   * "held → confirmed → issued → released"; that is display wording, not the stored value. Do not
   * "correct" this union to match the screen, and do not add a fifth value without a migration.
   */
  status: StockReservationStatus;
  reservedAt: string | null;
  releasedAt: string | null;
  usedAt: string | null;
  createdAt: string;
  // CW-07: what it is for and when it is needed (filled by the list endpoint only).
  bookingRequestId?: string | null;
  requestObjective?: string | null;
  studentName?: string | null;
  roomName?: string | null;
  slotStartsAt?: string | null;
  slotEndsAt?: string | null;
}

/** Mirrors `suppliers`. `contactEmail` is citext in the database — casing does not create a second row. */
export interface Supplier {
  id: string;
  name: string;
  contactEmail: string;
  phone: string;
  address: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface ListParams {
  page?: number;
  pageSize?: number;
  search?: string;
  sortBy?: string;
  sortDir?: "asc" | "desc";
}

function buildQuery(params: object): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params) as [string, string | number | undefined][]) {
    if (value !== undefined && value !== "") search.set(key, String(value));
  }
  const qs = search.toString();
  return qs ? `?${qs}` : "";
}

/** sortBy: name | unitPrice | stockQuantity | createdAt. `activeOnly` defaults to true on the API. */
export function listConsumables(
  token: string,
  params: ListParams & { activeOnly?: boolean } = {},
): Promise<PagedResult<Consumable>> {
  return apiFetch(`/api/consumables${buildQuery(params)}`, { token });
}

export function getConsumable(token: string, id: string): Promise<ConsumableDetail> {
  return apiFetch(`/api/consumables/${id}`, { token });
}

/** Every active item at or below its reorder level — a plain list, not paged. */
export function listLowStock(token: string): Promise<Consumable[]> {
  return apiFetch(`/api/consumables/low-stock`, { token });
}

/** `availableQuantity` is generated and `reservedQuantity` is moved by reservations, so neither is
 * part of a create or update body. Stock arrives through `stockIn`, not through this. */
export type ConsumableWriteBody = Pick<
  Consumable,
  "name" | "description" | "unit" | "unitPrice" | "minStockLevel"
>;

export function createConsumable(token: string, body: ConsumableWriteBody): Promise<Consumable> {
  return apiFetch(`/api/consumables`, { method: "POST", token, body });
}

/** StoreOfficer only. `isActive` deactivates or reactivates the item (CW-05); left out, it is unchanged. */
export function updateConsumable(
  token: string,
  id: string,
  body: ConsumableWriteBody & { isActive?: boolean },
): Promise<Consumable> {
  return apiFetch(`/api/consumables/${id}`, { method: "PUT", token, body });
}

/** Admin only. A deactivation, not a physical delete. */
export function deactivateConsumable(token: string, id: string): Promise<void> {
  return apiFetch(`/api/consumables/${id}`, { method: "DELETE", token });
}

/** Business operation: also writes a stock_transactions row, not just a balance change. Quantity must be > 0. */
export function stockIn(token: string, id: string, quantity: number, notes?: string): Promise<Consumable> {
  return apiFetch(`/api/consumables/${id}/stock-in`, { method: "POST", token, body: { quantity, notes } });
}

/** sortBy: createdAt | status | consumable. `status` must be one of the four stored values;
 * `dueOn` (YYYY-MM-DD, Colombo day) keeps reservations whose booked room slot starts that day. */
export function listStockReservations(
  token: string,
  params: ListParams & { status?: StockReservationStatus; dueOn?: string } = {},
): Promise<PagedResult<StockReservation>> {
  return apiFetch(`/api/stock-reservations${buildQuery(params)}`, { token });
}

/** Reserves one booking-request line. Transactional; 409 when stock is short or it is already reserved. */
export function createStockReservation(token: string, bookingRequestItemId: string): Promise<StockReservation> {
  return apiFetch(`/api/stock-reservations`, { method: "POST", token, body: { bookingRequestItemId } });
}

/** Only a Reserved reservation can be released (409 otherwise). */
export function releaseStockReservation(token: string, id: string): Promise<StockReservation> {
  return apiFetch(`/api/stock-reservations/${id}/release`, { method: "PUT", token });
}

/** Marks a Reserved reservation as issued: the stock leaves the store. */
export function markStockReservationUsed(token: string, id: string): Promise<StockReservation> {
  return apiFetch(`/api/stock-reservations/${id}/use`, { method: "PUT", token });
}

/** The append-only ledger. sortBy: createdAt | transactionType. */
export function listStockTransactions(
  token: string,
  params: ListParams & { consumableId?: string } = {},
): Promise<PagedResult<StockTransaction>> {
  return apiFetch(`/api/stock-transactions${buildQuery(params)}`, { token });
}

/** sortBy: name | createdAt. `activeOnly` defaults to true on the API. */
export function listSuppliers(
  token: string,
  params: ListParams & { activeOnly?: boolean } = {},
): Promise<PagedResult<Supplier>> {
  return apiFetch(`/api/suppliers${buildQuery(params)}`, { token });
}

export type SupplierWriteBody = Pick<Supplier, "name" | "contactEmail" | "phone" | "address">;

export function createSupplier(token: string, body: SupplierWriteBody): Promise<Supplier> {
  return apiFetch(`/api/suppliers`, { method: "POST", token, body });
}

export function updateSupplier(
  token: string,
  id: string,
  body: SupplierWriteBody & { isActive: boolean },
): Promise<Supplier> {
  return apiFetch(`/api/suppliers/${id}`, { method: "PUT", token, body });
}

/** One consumable's movements inside the report range (`ConsumableUsageRowResponse`). */
export interface ConsumableUsageRow {
  consumableId: string;
  name: string;
  unit: string;
  unitPrice: number;
  /** Units issued (reservations marked used) in the range. */
  issued: number;
  /** issued × the current unit price — the ledger stores quantities, not prices. */
  cost: number;
  reserved: number;
  released: number;
  stockedIn: number;
  stockQuantity: number;
  reservedNow: number;
  availableQuantity: number;
  isLowStock: boolean;
}

export interface ConsumableLowStock {
  consumableId: string;
  name: string;
  unit: string;
  stockQuantity: number;
  reservedQuantity: number;
  availableQuantity: number;
  minStockLevel: number;
}

export interface ConsumableUsageReport {
  from: string;
  to: string;
  totalIssued: number;
  totalCost: number;
  totalReserved: number;
  totalReleased: number;
  totalStockedIn: number;
  byItem: PagedResult<ConsumableUsageRow>;
  lowStock: ConsumableLowStock[];
}

/** W-24, StoreOfficer only. `from`/`to` are ISO-8601 instants; sortBy: issued | cost | reserved | released | name. */
export function getConsumableUsageReport(
  token: string,
  params: ListParams & { from?: string; to?: string } = {},
): Promise<ConsumableUsageReport> {
  return apiFetch(`/api/reports/consumable-usage${buildQuery(params)}`, { token });
}
