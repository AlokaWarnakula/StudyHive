import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Screen } from "../../components/AppShell";
import { MetricTiles } from "../../components/ui";
import { listLowStock, type Consumable } from "../../api/consumables";
import { useAuthStore } from "../../store/authStore";
import { StockTag } from "./shared";
import { messageOf, money } from "./storeUtils";

/**
 * W-21 · Low stock — GET /api/consumables/low-stock: every active item at or below its reorder
 * level, emptiest first. Each row opens the item, where stock-in happens. Owned by S3.
 */
export function LowStockPage() {
  const token = useAuthStore((s) => s.accessToken);
  const navigate = useNavigate();
  const [items, setItems] = useState<Consumable[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!token) return;
    let cancelled = false;
    listLowStock(token)
      .then((data) => {
        if (!cancelled) setItems(data);
      })
      .catch((err) => {
        if (!cancelled) setError(messageOf(err, "Failed to load low-stock items."));
      });
    return () => {
      cancelled = true;
    };
  }, [token]);

  const rows = items ?? [];
  const outOfStock = rows.filter((c) => c.stockQuantity === 0).length;
  // Restocking to twice the reorder level is the suggestion; the shortfall is what is needed just
  // to get back above it.
  const suggested = (c: Consumable) => Math.max(c.minStockLevel * 2 - c.stockQuantity, 1);
  const reorderValue = rows.reduce((sum, c) => sum + suggested(c) * c.unitPrice, 0);

  return (
    <Screen
      title="Low stock"
      crumb={items ? `${rows.length} items at or below their reorder level` : undefined}
      onBack={() => navigate("/consumables")}
      showUser={false}
    >
      {error && (
        <p role="alert" className="form-error">
          {error}
        </p>
      )}

      {!items && !error && <div className="state-view">Loading…</div>}

      {items && (
        <MetricTiles
          columns={3}
          metrics={[
            { label: "At or below reorder", value: String(rows.length) },
            { label: "Out of stock", value: String(outOfStock), highlight: outOfStock > 0 },
            { label: "Value to reorder", value: money(reorderValue), note: "at current unit prices" },
          ]}
        />
      )}

      {items && rows.length === 0 && <div className="state-view">Every item is above its reorder level.</div>}

      {rows.length > 0 && (
        <div className="table-scroll">
          <table className="table">
            <thead>
              <tr>
                <th>Item</th>
                <th>In stock</th>
                <th>Reserved</th>
                <th>Reorder at</th>
                <th>Shortfall</th>
                <th>Suggested order</th>
                <th>Status</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {rows.map((c) => (
                <tr key={c.id}>
                  <td>
                    <b>{c.name}</b>
                    <div className="fnote">{c.unit}</div>
                  </td>
                  <td>
                    <b style={c.stockQuantity === 0 ? { color: "var(--color-accent-700)" } : undefined}>{c.stockQuantity}</b>
                  </td>
                  <td>{c.reservedQuantity}</td>
                  <td>{c.minStockLevel}</td>
                  <td>{Math.max(c.minStockLevel - c.stockQuantity, 0)}</td>
                  <td>
                    {suggested(c)} {c.unit} · {money(suggested(c) * c.unitPrice)}
                  </td>
                  <td>
                    <StockTag item={c} />
                  </td>
                  <td>
                    {/* The out-of-stock items are the ones to act on first, so only they get the primary button. */}
                    <button
                      type="button"
                      className={c.stockQuantity === 0 ? "btn btn-primary" : "btn btn-secondary"}
                      onClick={() => navigate(`/consumables/${c.id}`)}
                    >
                      Stock in
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Screen>
  );
}
