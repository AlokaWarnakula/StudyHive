/**
 * S3 — Consumables & Stock API client. SCAFFOLD.
 *
 * Typed and written; the endpoints behind it return 501 until S3 implements
 * `api/src/StudyHive.Api/Controllers/Store/`. Calling one today throws an `ApiError` with status
 * 501 — the honest answer. Nothing here fabricates data.
 *
 * Screens this backs: W-19 Consumables, W-20 Consumable detail + stock-in, W-21 Low stock,
 * W-22 Stock reservations, W-23 Suppliers; mobile browse/detail/select consumables.
 *
 * S3, to switch a screen on: replace the page's `useFixture` call with `useState` + `useEffect`
 * against these functions. `web/src/pages/requests/RequestsPage.tsx` is the working reference.
 *
 * The one that carries your marks is `createStockReservation`: the plan requires it to be
 * transactional and to hold under concurrent callers without overselling. That is S3's business
 * operation, and the concurrency test has to genuinely pass.
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

/** W-20: the consumable plus its ledger rows. */
export interface ConsumableDetail extends Consumable {
  ledger: StockTransaction[];
}

export interface StockReservation {
  id: string;
  bookingRequestItemId: string;
  consumableId: string;
  quantity: number;
  /**
   * These four values are the ones the database will accept — see the CHECK constraint on
   * `stock_reservations.status` in DATABASE.md. The W-22 screen labels the same lifecycle as
   * "held → confirmed → issued → released"; that is display wording, not the stored value. Do not
   * "correct" this union to match the screen, and do not add a fifth value without a migration.
   */
  status: "Pending" | "Reserved" | "Released" | "Used";
  createdAt: string;
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

// TODO(S3): implement GET /api/consumables
export function listConsumables(token: string, params: ListParams = {}): Promise<PagedResult<Consumable>> {
  return apiFetch(`/api/consumables${buildQuery(params)}`, { token });
}

// TODO(S3): implement GET /api/consumables/{id}
export function getConsumable(token: string, id: string): Promise<ConsumableDetail> {
  return apiFetch(`/api/consumables/${id}`, { token });
}

// TODO(S3): implement GET /api/consumables/low-stock
export function listLowStock(token: string): Promise<PagedResult<Consumable>> {
  return apiFetch(`/api/consumables/low-stock`, { token });
}

/** `availableQuantity` is generated and `reservedQuantity` is moved by reservations, so neither is
 * part of a create or update body. Stock arrives through `stockIn`, not through this. */
export type ConsumableWriteBody = Pick<
  Consumable,
  "name" | "description" | "unit" | "unitPrice" | "minStockLevel"
>;

// TODO(S3): implement POST /api/consumables
export function createConsumable(token: string, body: ConsumableWriteBody): Promise<Consumable> {
  return apiFetch(`/api/consumables`, { method: "POST", token, body });
}

// TODO(S3): implement PUT /api/consumables/{id}
export function updateConsumable(token: string, id: string, body: ConsumableWriteBody): Promise<Consumable> {
  return apiFetch(`/api/consumables/${id}`, { method: "PUT", token, body });
}

// TODO(S3): implement DELETE /api/consumables/{id} — deactivation, not a physical delete
export function deactivateConsumable(token: string, id: string): Promise<void> {
  return apiFetch(`/api/consumables/${id}`, { method: "DELETE", token });
}

/** Business operation: also writes a stock_transactions row, not just a balance change. */
// TODO(S3): implement POST /api/consumables/{id}/stock-in
export function stockIn(token: string, id: string, quantity: number, note?: string): Promise<Consumable> {
  return apiFetch(`/api/consumables/${id}/stock-in`, { method: "POST", token, body: { quantity, note } });
}

// TODO(S3): implement GET /api/stock-reservations
export function listStockReservations(token: string, params: ListParams = {}): Promise<PagedResult<StockReservation>> {
  return apiFetch(`/api/stock-reservations${buildQuery(params)}`, { token });
}

/** Must be transactional and must not oversell under concurrent callers. */
// TODO(S3): implement POST /api/stock-reservations
export function createStockReservation(
  token: string,
  body: { bookingRequestId: string; items: { consumableId: string; quantity: number }[] },
): Promise<StockReservation[]> {
  return apiFetch(`/api/stock-reservations`, { method: "POST", token, body });
}

// TODO(S3): implement PUT /api/stock-reservations/{id}/release
export function releaseStockReservation(token: string, id: string): Promise<StockReservation> {
  return apiFetch(`/api/stock-reservations/${id}/release`, { method: "PUT", token });
}

// TODO(S3): implement PUT /api/stock-reservations/{id}/use
export function useStockReservation(token: string, id: string): Promise<StockReservation> {
  return apiFetch(`/api/stock-reservations/${id}/use`, { method: "PUT", token });
}

// TODO(S3): implement GET /api/stock-transactions
export function listStockTransactions(token: string, params: ListParams = {}): Promise<PagedResult<StockTransaction>> {
  return apiFetch(`/api/stock-transactions${buildQuery(params)}`, { token });
}

// TODO(S3): implement GET /api/suppliers
export function listSuppliers(token: string, params: ListParams = {}): Promise<PagedResult<Supplier>> {
  return apiFetch(`/api/suppliers${buildQuery(params)}`, { token });
}

export type SupplierWriteBody = Pick<Supplier, "name" | "contactEmail" | "phone" | "address">;

// TODO(S3): implement POST /api/suppliers
export function createSupplier(token: string, body: SupplierWriteBody): Promise<Supplier> {
  return apiFetch(`/api/suppliers`, { method: "POST", token, body });
}

// TODO(S3): implement PUT /api/suppliers/{id}
export function updateSupplier(token: string, id: string, body: SupplierWriteBody): Promise<Supplier> {
  return apiFetch(`/api/suppliers/${id}`, { method: "PUT", token, body });
}
