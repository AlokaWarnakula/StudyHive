import { useState } from "react";
import type { Consumable, ConsumableWriteBody } from "../../api/consumables";
import { Dialog, Field, Tag } from "../../components/ui";
import { messageOf, validateConsumableForm, type ConsumableFormState } from "./storeUtils";

/** Components shared by the S3 store screens (W-19 … W-24). */

export function StockTag({ item }: { item: Pick<Consumable, "stockQuantity" | "minStockLevel"> }) {
  if (item.stockQuantity === 0) return <Tag tone="outline">Out of stock</Tag>;
  if (item.stockQuantity <= item.minStockLevel) return <Tag tone="outline">Low</Tag>;
  return <Tag tone="accent">In stock</Tag>;
}

/** Add / edit dialog for W-19 and W-20. Stock is never edited here — it only moves through stock-in. */
export function ConsumableFormDialog({
  initial,
  onClose,
  onSave,
}: {
  initial: Consumable | null;
  onClose: () => void;
  onSave: (body: ConsumableWriteBody) => Promise<void>;
}) {
  const [form, setForm] = useState<ConsumableFormState>({
    name: initial?.name ?? "",
    description: initial?.description ?? "",
    unit: initial?.unit ?? "",
    unitPrice: initial ? String(initial.unitPrice) : "",
    minStockLevel: initial ? String(initial.minStockLevel) : "0",
  });
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function save() {
    const result = validateConsumableForm(form);
    if ("error" in result) {
      setError(result.error);
      return;
    }
    setError(null);
    setSaving(true);
    try {
      await onSave(result.body);
    } catch (err) {
      setError(messageOf(err, "Failed to save the item."));
      setSaving(false);
    }
  }

  const set = (key: keyof ConsumableFormState) => (value: string) => setForm((f) => ({ ...f, [key]: value }));

  return (
    <Dialog
      title={initial ? `Edit ${initial.name}` : "Add item"}
      body={initial ? undefined : "New items start at 0 in stock. Add the first delivery with Stock in."}
      width={500}
      onClose={onClose}
      actions={
        <>
          <button type="button" className="btn btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button type="button" className="btn btn-primary" onClick={save} disabled={saving}>
            {saving ? "Saving…" : "Save item"}
          </button>
        </>
      }
    >
      <Field label="Name">
        <input className="input" aria-label="Name" value={form.name} onChange={(e) => set("name")(e.target.value)} />
      </Field>
      <Field label="Description">
        <textarea
          className="input"
          aria-label="Description"
          value={form.description}
          onChange={(e) => set("description")(e.target.value)}
        />
      </Field>
      <div className="k2">
        <Field label="Unit">
          <input
            className="input"
            aria-label="Unit"
            placeholder="pcs, box, ream…"
            value={form.unit}
            onChange={(e) => set("unit")(e.target.value)}
          />
        </Field>
        <Field label="Unit price (Rs.)">
          <input
            className="input"
            aria-label="Unit price (Rs.)"
            inputMode="decimal"
            value={form.unitPrice}
            onChange={(e) => set("unitPrice")(e.target.value)}
          />
        </Field>
      </div>
      <Field label="Reorder level">
        <input
          className="input"
          aria-label="Reorder level"
          inputMode="numeric"
          value={form.minStockLevel}
          onChange={(e) => set("minStockLevel")(e.target.value)}
        />
      </Field>
      {error && (
        <p role="alert" className="form-error">
          {error}
        </p>
      )}
    </Dialog>
  );
}
