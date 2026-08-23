import { useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { Screen } from "../../components/AppShell";
import {
  Dialog,
  Field,
  FixtureNotice,
  KeyValue,
  Meter,
  NotBuiltYet,
  Pagination,
  TagOf,
  TextField,
  Tile,
} from "../../components/ui";
import { useFixture } from "../../dev/useFixture";

/**
 * W-20 · Consumable detail and the stock-in dialog.
 * GET /api/consumables/{id} + its ledger · POST /api/consumables/{id}/stock-in. Owned by S3.
 */
export function ConsumableDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const fixture = useFixture((f) => ({ item: f.consumableDetail, form: f.forms.stockIn }));
  const [dialogOpen, setDialogOpen] = useState(false);
  const [quantity, setQuantity] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  if (!fixture.enabled) {
    return (
      <Screen title={id ?? "Consumable"} crumb="Consumables" onBack={() => navigate("/consumables")}>
        <NotBuiltYet owner="S3 store" what="Consumable detail and its ledger" />
      </Screen>
    );
  }

  const c = fixture.data.item;
  const form = fixture.data.form;
  // `quantity` starts null so the reference's prefilled value can come from the fixture.
  const quantityValue = quantity ?? form.quantity;

  function save() {
    const n = Number(quantityValue);
    // The dialog's own rule: a stock-in adds to the shelf and stock can never go negative.
    if (!Number.isInteger(n) || n <= 0) {
      setFormError("Quantity received must be a whole number greater than 0.");
      return;
    }
    setFormError(null);
    setDialogOpen(false);
  }

  return (
    <Screen
      title={c.name}
      crumb={`Consumables / ${id ?? c.code}`}
      onBack={() => navigate("/consumables")}
      showUser={false}
      actions={
        <>
          <button type="button" className="btn btn-secondary">
            Edit item
          </button>
          <button type="button" className="btn btn-primary" onClick={() => setDialogOpen(true)}>
            Stock in
          </button>
        </>
      }
    >
      <FixtureNotice owner="S3" what="This item's stock and movement ledger" />

      <div className="split" style={{ gridTemplateColumns: "320px 1fr" }}>
        <div className="stack">
          <Tile>
            {c.facts.map((f) => (
              <KeyValue key={f.label} label={f.label}>
                {f.value}
              </KeyValue>
            ))}
            <KeyValue label="Status">
              <TagOf tag={c.status} />
            </KeyValue>
          </Tile>

          <Tile label="Stock level">
            <Meter percent={c.stockPercent} />
            <span className="fnote">{c.stockNote}</span>
          </Tile>
        </div>

        <Tile label="Stock movements (ledger, append-only)">
          <div className="table-scroll">
            <table className="table">
              <thead>
                <tr>
                  <th>When</th>
                  <th>Type</th>
                  <th>Qty</th>
                  <th>Balance</th>
                  <th>Reference</th>
                  <th>By</th>
                </tr>
              </thead>
              <tbody>
                {c.ledger.map((l, i) => (
                  <tr key={`${l.when}-${i}`}>
                    <td>{l.when}</td>
                    <td>
                      <TagOf tag={l.type} />
                    </td>
                    <td>{l.qty}</td>
                    <td>{l.balance}</td>
                    <td>{l.reference}</td>
                    <td>{l.by}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination showing={`Showing 1–${c.ledger.length} of ${c.ledgerTotal} movements`} disablePrevious />
        </Tile>
      </div>

      {dialogOpen && (
        <Dialog
          title={`Stock in — ${c.name}`}
          body="Adds to the shelf and writes one ledger row. Stock cannot go negative."
          width={460}
          onClose={() => setDialogOpen(false)}
          actions={
            <>
              <button type="button" className="btn btn-secondary" onClick={() => setDialogOpen(false)}>
                Cancel
              </button>
              <button type="button" className="btn btn-primary" onClick={save}>
                Save stock in
              </button>
            </>
          }
        >
          <div className="k2">
            <Field label="Quantity received">
              <input
                className="input"
                value={quantityValue}
                aria-label="Quantity received"
                onChange={(e) => setQuantity(e.target.value)}
              />
            </Field>
            <TextField label="Unit cost (Rs.)" defaultValue={form.unitCost} />
            <Field label="Supplier">
              <select className="input" aria-label="Supplier">
                {form.suppliers.map((s) => (
                  <option key={s}>{s}</option>
                ))}
              </select>
            </Field>
            <TextField label="Purchase order" defaultValue={form.purchaseOrder} />
          </div>
          <TextField label="Note" placeholder="Optional" />

          <div className="kv" style={{ borderTop: "1px solid var(--color-divider)", paddingTop: 10 }}>
            <span>New balance</span>
            <b>
              {Number.isFinite(Number(quantityValue))
                ? form.balanceBefore + Number(quantityValue)
                : form.balanceBefore}{" "}
              in stock
            </b>
          </div>

          {formError && (
            <p role="alert" className="form-error">
              {formError}
            </p>
          )}
        </Dialog>
      )}
    </Screen>
  );
}
