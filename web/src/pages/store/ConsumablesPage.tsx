import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Screen } from "../../components/AppShell";
import { Icon } from "../../components/Icon";
import { Pagination, Toolbar } from "../../components/ui";
import {
  createConsumable,
  listConsumables,
  listLowStock,
  updateConsumable,
  type Consumable,
  type PagedResult,
} from "../../api/consumables";
import { can } from "../../auth/permissions";
import { useAuthStore } from "../../store/authStore";
import { ConsumableFormDialog, StockTag } from "./shared";
import { messageOf, money } from "./storeUtils";

type SortBy = "name" | "unitPrice" | "stockQuantity";
type StockLevel = "all" | "low" | "out";

const PAGE_SIZE = 20;

/**
 * W-19 · Consumables — GET /api/consumables with search, sort and pagination, plus a stock-level
 * filter. "Low" and "Out of stock" read GET /api/consumables/low-stock (the whole at-or-below-reorder
 * list, which the API does not page) and are searched and sorted here. Add / edit through
 * POST / PUT /api/consumables. Owned by S3.
 */
export function ConsumablesPage() {
  const token = useAuthStore((s) => s.accessToken);
  const role = useAuthStore((s) => s.user?.role);
  const navigate = useNavigate();

  const [result, setResult] = useState<PagedResult<Consumable> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState("");
  const [stockLevel, setStockLevel] = useState<StockLevel>("all");
  const [sortBy, setSortBy] = useState<SortBy>("name");
  const [sortDir, setSortDir] = useState<"asc" | "desc">("asc");
  const [page, setPage] = useState(1);
  const [reload, setReload] = useState(0);

  const [dialog, setDialog] = useState<{ editing: Consumable | null } | null>(null);

  useEffect(() => {
    if (!token) return;
    let cancelled = false;
    setLoading(true);
    setError(null);

    const request =
      stockLevel === "all"
        ? listConsumables(token, { page, pageSize: PAGE_SIZE, search: search || undefined, sortBy, sortDir })
        : listLowStock(token).then((items) => lowStockPage(items, stockLevel, search, sortBy, sortDir));

    request
      .then((data) => {
        if (!cancelled) setResult(data);
      })
      .catch((err) => {
        if (!cancelled) setError(messageOf(err, "Failed to load consumables."));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [token, page, search, stockLevel, sortBy, sortDir, reload]);

  function toggleSort(column: SortBy) {
    if (sortBy === column) {
      setSortDir((d) => (d === "asc" ? "desc" : "asc"));
    } else {
      setSortBy(column);
      setSortDir("asc");
    }
    setPage(1);
  }

  function sortMark(column: SortBy) {
    return sortBy === column ? (sortDir === "asc" ? "▲" : "▼") : "";
  }

  async function save(body: Parameters<typeof createConsumable>[1]) {
    if (!token || !dialog) return;
    if (dialog.editing) await updateConsumable(token, dialog.editing.id, body);
    else await createConsumable(token, body);
    setDialog(null);
    setReload((n) => n + 1);
  }

  const items = result?.items ?? [];
  const firstRow = result && result.totalItems > 0 ? (result.page - 1) * result.pageSize + 1 : 0;
  // The API lets StoreOfficer and Admin create, but only StoreOfficer edit.
  const canEdit = can(role, "consumables.edit");

  return (
    <Screen
      title="Consumables"
      crumb={result ? `${result.totalItems} ${stockLevel === "all" ? "active items" : "shown"}` : undefined}
      actions={
        <>
          {can(role, "lowStock.view") && (
            <button type="button" className="btn btn-secondary" onClick={() => navigate("/consumables/low-stock")}>
              Low stock
            </button>
          )}
          {can(role, "consumables.create") && <button type="button" className="btn btn-primary" onClick={() => setDialog({ editing: null })}>
            <Icon name="plus" size={16} />
            Add item
          </button>}
        </>
      }
    >
      <Toolbar>
        <input
          className="input"
          style={{ maxWidth: 260 }}
          type="search"
          placeholder="Search item name"
          aria-label="Search item name"
          value={search}
          onChange={(e) => {
            setPage(1);
            setSearch(e.target.value);
          }}
        />
        <select
          className="input"
          style={{ maxWidth: 200 }}
          aria-label="Stock level"
          value={stockLevel}
          onChange={(e) => {
            setPage(1);
            setStockLevel(e.target.value as StockLevel);
          }}
        >
          <option value="all">Stock level: All</option>
          <option value="low">Stock level: At or below reorder</option>
          <option value="out">Stock level: Out of stock</option>
        </select>
      </Toolbar>

      {error && (
        <p role="alert" className="form-error">
          {error}
        </p>
      )}

      {loading && !result && <div className="state-view">Loading…</div>}

      {result && items.length === 0 && !loading && (
        <div className="state-view">No consumable matches these filters.</div>
      )}

      {items.length > 0 && (
        <>
          <div className="table-scroll">
            <table className="table">
              <thead>
                <tr>
                  <th>
                    <button type="button" onClick={() => toggleSort("name")}>
                      Item {sortMark("name")}
                    </button>
                  </th>
                  <th>
                    <button type="button" onClick={() => toggleSort("unitPrice")}>
                      Unit price {sortMark("unitPrice")}
                    </button>
                  </th>
                  <th>
                    <button type="button" onClick={() => toggleSort("stockQuantity")}>
                      In stock {sortMark("stockQuantity")}
                    </button>
                  </th>
                  <th>Reserved</th>
                  <th>Free</th>
                  <th>Reorder at</th>
                  <th>Status</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {items.map((c) => (
                  <tr key={c.id} className="row-click" onClick={() => navigate(`/consumables/${c.id}`)}>
                    <td>
                      <b>{c.name}</b>
                      <div className="fnote">{c.unit}</div>
                    </td>
                    <td>{money(c.unitPrice)}</td>
                    {/* An item at zero is the point of this screen, so it is called out in the accent colour. */}
                    <td style={c.stockQuantity === 0 ? { color: "var(--color-accent-700)", fontWeight: 500 } : undefined}>
                      {c.stockQuantity}
                    </td>
                    <td>{c.reservedQuantity}</td>
                    <td>{c.availableQuantity}</td>
                    <td>{c.minStockLevel}</td>
                    <td>
                      <StockTag item={c} />
                    </td>
                    <td>
                      {canEdit && (
                        <button
                          type="button"
                          className="btn btn-ghost"
                          onClick={(e) => {
                            e.stopPropagation();
                            setDialog({ editing: c });
                          }}
                        >
                          Edit
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <Pagination
            showing={`Showing ${firstRow}–${firstRow + items.length - 1} of ${result!.totalItems}`}
            disablePrevious={result!.page <= 1}
            disableNext={result!.page >= result!.totalPages}
            onPrevious={() => setPage((p) => p - 1)}
            onNext={() => setPage((p) => p + 1)}
          />
        </>
      )}

      {dialog && <ConsumableFormDialog initial={dialog.editing} onClose={() => setDialog(null)} onSave={save} />}
    </Screen>
  );
}

/** The low-stock list is small and unpaged, so the stock-level views filter and sort it here and
 * present it as a single page. */
function lowStockPage(
  items: Consumable[],
  level: Exclude<StockLevel, "all">,
  search: string,
  sortBy: SortBy,
  sortDir: "asc" | "desc",
): PagedResult<Consumable> {
  const term = search.trim().toLowerCase();
  const rows = items
    .filter((c) => (level === "out" ? c.stockQuantity === 0 : true))
    .filter((c) => !term || c.name.toLowerCase().includes(term))
    .sort((a, b) => {
      const order = sortBy === "name" ? a.name.localeCompare(b.name) : a[sortBy] - b[sortBy];
      return sortDir === "asc" ? order : -order;
    });
  return { items: rows, page: 1, pageSize: Math.max(rows.length, 1), totalItems: rows.length, totalPages: 1 };
}
