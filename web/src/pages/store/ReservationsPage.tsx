import { useEffect, useState } from "react";
import { Screen } from "../../components/AppShell";
import { Pagination, Tag, Toolbar } from "../../components/ui";
import {
  listStockReservations,
  releaseStockReservation,
  markStockReservationUsed,
  type PagedResult,
  type StockReservation,
  type StockReservationStatus,
} from "../../api/consumables";
import { useAuthStore } from "../../store/authStore";
import { RESERVATION_STATUS_LABELS, messageOf } from "./storeUtils";

const STATUS_OPTIONS: StockReservationStatus[] = ["Pending", "Reserved", "Used", "Released"];

type SortBy = "createdAt" | "status";

const PAGE_SIZE = 20;

/**
 * W-22 · Stock reservations — GET /api/stock-reservations?status= with sort and pagination, and the
 * two store actions on a held reservation: PUT …/release (stock back on the shelf) and PUT …/use
 * (issued, stock leaves the store). The filter sends the database's four values; the screen's
 * held / issued wording is display only. Owned by S3.
 */
export function ReservationsPage() {
  const token = useAuthStore((s) => s.accessToken);
  const role = useAuthStore((s) => s.user?.role);

  const [result, setResult] = useState<PagedResult<StockReservation> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const [status, setStatus] = useState<StockReservationStatus | "">("");
  const [sortBy, setSortBy] = useState<SortBy>("createdAt");
  const [sortDir, setSortDir] = useState<"asc" | "desc">("desc");
  const [page, setPage] = useState(1);
  const [reload, setReload] = useState(0);

  useEffect(() => {
    if (!token) return;
    let cancelled = false;
    setLoading(true);
    setError(null);

    listStockReservations(token, { page, pageSize: PAGE_SIZE, status: status || undefined, sortBy, sortDir })
      .then((data) => {
        if (!cancelled) setResult(data);
      })
      .catch((err) => {
        if (!cancelled) setError(messageOf(err, "Failed to load stock reservations."));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [token, page, status, sortBy, sortDir, reload]);

  function toggleSort(column: SortBy) {
    if (sortBy === column) {
      setSortDir((d) => (d === "asc" ? "desc" : "asc"));
    } else {
      setSortBy(column);
      setSortDir("desc");
    }
    setPage(1);
  }

  function sortMark(column: SortBy) {
    return sortBy === column ? (sortDir === "asc" ? "▲" : "▼") : "";
  }

  async function act(reservation: StockReservation, action: "release" | "use") {
    if (!token) return;
    setBusyId(reservation.id);
    setActionError(null);
    try {
      if (action === "release") await releaseStockReservation(token, reservation.id);
      else await markStockReservationUsed(token, reservation.id);
      setReload((n) => n + 1);
    } catch (err) {
      setActionError(messageOf(err, "The reservation could not be updated."));
    } finally {
      setBusyId(null);
    }
  }

  const items = result?.items ?? [];
  const firstRow = result && result.totalItems > 0 ? (result.page - 1) * result.pageSize + 1 : 0;
  // Release and issue are StoreOfficer actions on the API; a Librarian can only look.
  const canAct = role === "StoreOfficer";

  return (
    <Screen title="Stock reservations" crumb={result ? `${result.totalItems} in total` : undefined}>
      <Toolbar>
        <select
          className="input"
          style={{ maxWidth: 220 }}
          aria-label="Status"
          value={status}
          onChange={(e) => {
            setPage(1);
            setStatus(e.target.value as StockReservationStatus | "");
          }}
        >
          <option value="">Status: All</option>
          {STATUS_OPTIONS.map((s) => (
            <option key={s} value={s}>
              {`Status: ${RESERVATION_STATUS_LABELS[s]}`}
            </option>
          ))}
        </select>
      </Toolbar>

      {error && (
        <p role="alert" className="form-error">
          {error}
        </p>
      )}
      {actionError && (
        <p role="alert" className="form-error">
          {actionError}
        </p>
      )}

      {loading && !result && <div className="state-view">Loading…</div>}

      {result && items.length === 0 && !loading && (
        <div className="state-view">No reservation matches this filter.</div>
      )}

      {items.length > 0 && (
        <>
          <div className="table-scroll">
            <table className="table">
              <thead>
                <tr>
                  <th>Reservation</th>
                  <th>Item</th>
                  <th>Qty</th>
                  <th>Held since</th>
                  <th>
                    <button type="button" onClick={() => toggleSort("status")}>
                      Status {sortMark("status")}
                    </button>
                  </th>
                  <th>
                    <button type="button" onClick={() => toggleSort("createdAt")}>
                      Created {sortMark("createdAt")}
                    </button>
                  </th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {items.map((r) => (
                  <tr key={r.id}>
                    <td>
                      <b>{r.id.slice(0, 8)}</b>
                    </td>
                    <td>{r.consumableName}</td>
                    <td>{r.quantity}</td>
                    <td>{r.reservedAt ? new Date(r.reservedAt).toLocaleString() : "—"}</td>
                    <td>
                      <Tag tone={r.status === "Reserved" ? "accent" : r.status === "Pending" ? "outline" : "neutral"}>
                        {RESERVATION_STATUS_LABELS[r.status]}
                      </Tag>
                    </td>
                    <td>{new Date(r.createdAt).toLocaleString()}</td>
                    <td>
                      {canAct && r.status === "Reserved" && (
                        <span style={{ display: "flex", gap: 6 }}>
                          <button
                            type="button"
                            className="btn btn-secondary"
                            disabled={busyId === r.id}
                            onClick={() => act(r, "use")}
                          >
                            Issue
                          </button>
                          <button
                            type="button"
                            className="btn btn-ghost"
                            disabled={busyId === r.id}
                            onClick={() => act(r, "release")}
                          >
                            Release
                          </button>
                        </span>
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
    </Screen>
  );
}
