import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { Screen } from "../../components/AppShell";
import { Dialog, Field, KeyValue, Meter, Pagination, Tag, Tile } from "../../components/ui";
import {
  getConsumable,
  listStockTransactions,
  deactivateConsumable,
  stockIn,
  updateConsumable,
  type Consumable,
  type PagedResult,
  type StockTransaction,
} from "../../api/consumables";
import { can } from "../../auth/permissions";
import { useAuthStore } from "../../store/authStore";
import { ConsumableFormDialog, StockTag } from "./shared";
import { messageOf, money, wholeNumber } from "./storeUtils";

const LEDGER_PAGE_SIZE = 10;

const TRANSACTION_LABELS: Record<StockTransaction["transactionType"], string> = {
  StockIn: "Stock in",
  StockOut: "Issued",
  Reserve: "Reserved",
  Release: "Released",
  Adjust: "Adjusted",
};

/**
 * W-20 · Consumable detail and the stock-in dialog.
 * GET /api/consumables/{id} · GET /api/stock-transactions?consumableId= (the paged ledger) ·
 * POST /api/consumables/{id}/stock-in · PUT /api/consumables/{id}. Owned by S3.
 */
export function ConsumableDetailPage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const token = useAuthStore((s) => s.accessToken);
  const role = useAuthStore((s) => s.user?.role);

  const [item, setItem] = useState<Consumable | null>(null);
  const [itemError, setItemError] = useState<string | null>(null);
  const [ledger, setLedger] = useState<PagedResult<StockTransaction> | null>(null);
  const [ledgerError, setLedgerError] = useState<string | null>(null);
  const [ledgerPage, setLedgerPage] = useState(1);
  const [reload, setReload] = useState(0);

  const [stockInOpen, setStockInOpen] = useState(false);
  const [editOpen, setEditOpen] = useState(false);
  const [confirmActive, setConfirmActive] = useState(false);
  const [activeError, setActiveError] = useState<string | null>(null);
  const [changingActive, setChangingActive] = useState(false);

  useEffect(() => {
    if (!token || !id) return;
    let cancelled = false;
    setItemError(null);
    getConsumable(token, id)
      .then((data) => {
        if (!cancelled) setItem(data.consumable);
      })
      .catch((err) => {
        if (!cancelled) setItemError(messageOf(err, "Failed to load this item."));
      });
    return () => {
      cancelled = true;
    };
  }, [token, id, reload]);

  useEffect(() => {
    if (!token || !id || !can(role, "consumables.ledger")) return;
    let cancelled = false;
    setLedgerError(null);
    listStockTransactions(token, { consumableId: id, page: ledgerPage, pageSize: LEDGER_PAGE_SIZE, sortBy: "createdAt", sortDir: "desc" })
      .then((data) => {
        if (!cancelled) setLedger(data);
      })
      .catch((err) => {
        if (!cancelled) setLedgerError(messageOf(err, "Failed to load the stock ledger."));
      });
    return () => {
      cancelled = true;
    };
  }, [token, id, ledgerPage, reload, role]);

  function afterChange() {
    setLedgerPage(1);
    setReload((n) => n + 1);
  }

  const back = () => navigate("/consumables");

  /**
   * CW-05: a consumable can be retired from the web. A StoreOfficer toggles it with PUT isActive
   * (and can bring it back); an Admin deactivates with DELETE, which is the endpoint Admin may call.
   */
  async function setActive(next: boolean) {
    if (!token || !item) return;
    setChangingActive(true);
    setActiveError(null);
    try {
      if (can(role, "consumables.edit")) {
        await updateConsumable(token, item.id, {
          name: item.name, description: item.description, unit: item.unit,
          unitPrice: item.unitPrice, minStockLevel: item.minStockLevel, isActive: next,
        });
      } else {
        await deactivateConsumable(token, item.id);
      }
      setConfirmActive(false);
      afterChange();
    } catch (err) {
      setActiveError(messageOf(err, "The item could not be updated."));
    } finally {
      setChangingActive(false);
    }
  }

  if (!item) {
    return (
      <Screen title="Consumable" crumb="Consumables" onBack={back}>
        {itemError ? (
          <p role="alert" className="form-error">
            {itemError}
          </p>
        ) : (
          <div className="state-view">Loading…</div>
        )}
      </Screen>
    );
  }

  const stockPercent = item.minStockLevel > 0 ? Math.min(100, (item.stockQuantity / (item.minStockLevel * 2)) * 100) : 100;
  const ledgerItems = ledger?.items ?? [];
  const firstRow = ledger && ledger.totalItems > 0 ? (ledger.page - 1) * ledger.pageSize + 1 : 0;

  return (
    <Screen
      title={item.name}
      crumb="Consumables"
      onBack={back}
      actions={
        <>
          {can(role, "consumables.deactivate") && (item.isActive || can(role, "consumables.edit")) && (
            <button type="button" className="btn btn-ghost" onClick={() => { setActiveError(null); setConfirmActive(true); }}>
              {item.isActive ? "Deactivate" : "Reactivate"}
            </button>
          )}
          {can(role, "consumables.edit") && (
            <button type="button" className="btn btn-secondary" onClick={() => setEditOpen(true)}>
              Edit item
            </button>
          )}
          {can(role, "consumables.stockIn") && (
            <button type="button" className="btn btn-primary" onClick={() => setStockInOpen(true)}>
              Stock in
            </button>
          )}
        </>
      }
    >
      <div className="split" style={{ gridTemplateColumns: "320px 1fr" }}>
        <div className="stack">
          <Tile>
            <KeyValue label="Unit">{item.unit}</KeyValue>
            <KeyValue label="Unit price">{money(item.unitPrice)}</KeyValue>
            <KeyValue label="In stock">{item.stockQuantity}</KeyValue>
            <KeyValue label="Reserved">{item.reservedQuantity}</KeyValue>
            <KeyValue label="Free to reserve">{item.availableQuantity}</KeyValue>
            <KeyValue label="Reorder at">{item.minStockLevel}</KeyValue>
            <KeyValue label="Status">
              <StockTag item={item} />
              {!item.isActive && <Tag tone="neutral">Inactive</Tag>}
            </KeyValue>
            {item.description && <p className="fnote">{item.description}</p>}
          </Tile>

          <Tile label="Stock level">
            <Meter percent={stockPercent} />
            <span className="fnote">
              {item.stockQuantity} on hand · reorder at {item.minStockLevel}
            </span>
          </Tile>
        </div>

        <Tile label="Stock movements (ledger, append-only)">
          {ledgerError && (
            <p role="alert" className="form-error">
              {ledgerError}
            </p>
          )}
          {!ledger && !ledgerError && <div className="state-view">Loading…</div>}
          {ledger && ledgerItems.length === 0 && <div className="state-view">No stock movements yet.</div>}
          {ledgerItems.length > 0 && (
            <>
              <div className="table-scroll">
                <table className="table">
                  <thead>
                    <tr>
                      <th>When</th>
                      <th>Type</th>
                      <th>Qty</th>
                      <th>Balance</th>
                      <th>Note</th>
                    </tr>
                  </thead>
                  <tbody>
                    {ledgerItems.map((t) => (
                      <tr key={t.id}>
                        <td>{new Date(t.createdAt).toLocaleString()}</td>
                        <td>
                          <Tag tone={t.transactionType === "StockIn" ? "accent" : "outline"}>
                            {TRANSACTION_LABELS[t.transactionType]}
                          </Tag>
                        </td>
                        <td>{t.quantity > 0 ? `+${t.quantity}` : t.quantity}</td>
                        <td>{t.balanceAfter}</td>
                        <td>{t.notes ?? "—"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <Pagination
                showing={`Showing ${firstRow}–${firstRow + ledgerItems.length - 1} of ${ledger!.totalItems} movements`}
                disablePrevious={ledger!.page <= 1}
                disableNext={ledger!.page >= ledger!.totalPages}
                onPrevious={() => setLedgerPage((p) => p - 1)}
                onNext={() => setLedgerPage((p) => p + 1)}
              />
            </>
          )}
        </Tile>
      </div>

      {stockInOpen && token && (
        <StockInDialog
          item={item}
          onClose={() => setStockInOpen(false)}
          onSave={async (quantity, notes) => {
            await stockIn(token, item.id, quantity, notes);
            setStockInOpen(false);
            afterChange();
          }}
        />
      )}

      {confirmActive && (
        <Dialog
          title={item.isActive ? `Deactivate ${item.name}?` : `Reactivate ${item.name}?`}
          onClose={() => setConfirmActive(false)}
          actions={
            <>
              <button type="button" className="btn btn-secondary" onClick={() => setConfirmActive(false)}>
                Cancel
              </button>
              <button type="button" className="btn btn-primary" disabled={changingActive} onClick={() => setActive(!item.isActive)}>
                {item.isActive ? "Deactivate" : "Reactivate"}
              </button>
            </>
          }
        >
          {activeError && (
            <p role="alert" className="form-error">
              {activeError}
            </p>
          )}
          <p style={{ margin: 0 }}>
            {item.isActive
              ? "Students can no longer pick it for a request. Existing reservations are not affected."
              : "Students can pick it for requests again."}
          </p>
        </Dialog>
      )}
      {editOpen && token && (
        <ConsumableFormDialog
          initial={item}
          onClose={() => setEditOpen(false)}
          onSave={async (body) => {
            await updateConsumable(token, item.id, body);
            setEditOpen(false);
            afterChange();
          }}
        />
      )}
    </Screen>
  );
}

function StockInDialog({
  item,
  onClose,
  onSave,
}: {
  item: Consumable;
  onClose: () => void;
  onSave: (quantity: number, notes: string | undefined) => Promise<void>;
}) {
  const [quantity, setQuantity] = useState("");
  const [notes, setNotes] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const parsed = wholeNumber(quantity);

  async function save() {
    // Mirrors StockInRequest: a stock-in only ever adds, so the quantity must be at least 1.
    if (parsed === null || parsed <= 0) {
      setError("Quantity received must be a whole number greater than 0.");
      return;
    }
    if (notes.length > 500) {
      setError("Note must be 500 characters or fewer.");
      return;
    }
    setError(null);
    setSaving(true);
    try {
      await onSave(parsed, notes.trim() || undefined);
    } catch (err) {
      setError(messageOf(err, "Failed to record the stock-in."));
      setSaving(false);
    }
  }

  return (
    <Dialog
      title={`Stock in — ${item.name}`}
      body="Adds to the shelf and writes one ledger row. Stock cannot go negative."
      width={460}
      onClose={onClose}
      actions={
        <>
          <button type="button" className="btn btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button type="button" className="btn btn-primary" onClick={save} disabled={saving}>
            {saving ? "Saving…" : "Save stock in"}
          </button>
        </>
      }
    >
      <Field label="Quantity received">
        <input
          className="input"
          aria-label="Quantity received"
          inputMode="numeric"
          value={quantity}
          onChange={(e) => setQuantity(e.target.value)}
        />
      </Field>
      <Field label="Note">
        <input
          className="input"
          aria-label="Note"
          placeholder="Optional — supplier, delivery note…"
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
        />
      </Field>

      <div className="kv" style={{ borderTop: "1px solid var(--color-divider)", paddingTop: 10 }}>
        <span>New balance</span>
        <b>{item.stockQuantity + (parsed !== null && parsed > 0 ? parsed : 0)} in stock</b>
      </div>

      {error && (
        <p role="alert" className="form-error">
          {error}
        </p>
      )}
    </Dialog>
  );
}
