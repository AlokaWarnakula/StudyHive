import { ApiError } from "../../api/client";
import type { ConsumableWriteBody, StockReservationStatus } from "../../api/consumables";

/** Pure helpers shared by the S3 store screens (W-19 … W-24). */

export function messageOf(error: unknown, fallback: string): string {
  return error instanceof ApiError ? error.message : fallback;
}

export function money(value: number): string {
  return `Rs. ${value.toLocaleString("en-LK", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

/** The database's four reservation values, with the W-22 screen's wording for each. The value sent
 * to the API is always the key — see the reservation status note in DOCS/S2_S3_S4_UI_Interface_Map.md. */
export const RESERVATION_STATUS_LABELS: Record<StockReservationStatus, string> = {
  Pending: "Pending approval",
  Reserved: "Held",
  Used: "Issued",
  Released: "Released",
};

/** Parses a whole number from a form field; null when it is not one. */
export function wholeNumber(raw: string): number | null {
  const trimmed = raw.trim();
  if (!/^-?\d+$/.test(trimmed)) return null;
  return Number(trimmed);
}

export interface ConsumableFormState {
  name: string;
  description: string;
  unit: string;
  unitPrice: string;
  minStockLevel: string;
}

/** Client-side mirror of CreateConsumableRequest / UpdateConsumableRequest validation. */
export function validateConsumableForm(form: ConsumableFormState): { body: ConsumableWriteBody } | { error: string } {
  const name = form.name.trim();
  const unit = form.unit.trim();
  if (!name) return { error: "Name is required." };
  if (name.length > 120) return { error: "Name must be 120 characters or fewer." };
  if (!unit) return { error: "Unit is required." };
  if (unit.length > 20) return { error: "Unit must be 20 characters or fewer." };

  const price = Number(form.unitPrice.trim());
  if (form.unitPrice.trim() === "" || !Number.isFinite(price) || price < 0) {
    return { error: "Unit price must be a number of 0 or more." };
  }
  const minStock = wholeNumber(form.minStockLevel);
  if (minStock === null || minStock < 0) return { error: "Reorder level must be a whole number of 0 or more." };

  return {
    body: {
      name,
      description: form.description.trim() || null,
      unit,
      unitPrice: Math.round(price * 100) / 100,
      minStockLevel: minStock,
    },
  };
}
