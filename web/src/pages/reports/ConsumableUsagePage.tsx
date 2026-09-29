import { useEffect, useState } from "react";
import { Screen } from "../../components/AppShell";
import { MetricTiles, Pagination, Tag, Tile } from "../../components/ui";
import { getConsumableUsageReport, type ConsumableUsageReport } from "../../api/consumables";
import { useAuthStore } from "../../store/authStore";
import { messageOf, money } from "../store/storeUtils";

type SortBy = "issued" | "cost" | "reserved" | "released" | "name";

const PAGE_SIZE = 20;

function daysAgo(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() - days);
  return d.toISOString().slice(0, 10);
}

/**
 * W-24 · Consumable usage report — GET /api/reports/consumable-usage (StoreOfficer): units issued,
 * reserved, released and restocked per item over a date range, what that issue cost, and the
 * current low-stock list. The range is whole local days, sent as UTC instants. Owned by S3.
 */
export function ConsumableUsagePage() {
  const token = useAuthStore((s) => s.accessToken);
  const [fromDate, setFromDate] = useState(() => daysAgo(30));
  const [toDate, setToDate] = useState(() => daysAgo(0));
  const [sortBy, setSortBy] = useState<SortBy>("issued");
  const [sortDir, setSortDir] = useState<"asc" | "desc">("desc");
  const [page, setPage] = useState(1);

  const [report, setReport] = useState<ConsumableUsageReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const rangeError = fromDate && toDate && fromDate > toDate ? "The start date must be on or before the end date." : null;

  useEffect(() => {
    if (!token || !fromDate || !toDate || rangeError) return;
    let cancelled = false;
    // Local midnight at the start of `from` to local midnight after `to`, so both days are included.
    const from = new Date(`${fromDate}T00:00:00`);
    const to = new Date(`${toDate}T00:00:00`);
    to.setDate(to.getDate() + 1);
    setLoading(true);
    setError(null);

    getConsumableUsageReport(token, {
      from: from.toISOString(),
      to: to.toISOString(),
      page,
      pageSize: PAGE_SIZE,
      sortBy,
      sortDir,
    })
      .then((data) => {
        if (!cancelled) setReport(data);
      })
      .catch((err) => {
        if (!cancelled) setError(messageOf(err, "Failed to load the consumable usage report."));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [token, fromDate, toDate, rangeError, page, sortBy, sortDir]);

  function toggleSort(column: SortBy) {
    if (sortBy === column) {
      setSortDir((d) => (d === "asc" ? "desc" : "asc"));
    } else {
      setSortBy(column);
      setSortDir(column === "name" ? "asc" : "desc");
    }
    setPage(1);
  }

  function sortMark(column: SortBy) {
    return sortBy === column ? (sortDir === "asc" ? "▲" : "▼") : "";
  }

  const rows = report?.byItem.items ?? [];
  const firstRow = report && report.byItem.totalItems > 0 ? (report.byItem.page - 1) * report.byItem.pageSize + 1 : 0;

  return (
    <Screen
      title="Consumable usage"
      showUser={false}
      actions={
        <>
          <input
            className="input"
            type="date"
            style={{ width: 160 }}
            aria-label="From"
            value={fromDate}
            onChange={(e) => {
              setPage(1);
              setFromDate(e.target.value);
            }}
          />
          <input
            className="input"
            type="date"
            style={{ width: 160 }}
            aria-label="To"
            value={toDate}
            onChange={(e) => {
              setPage(1);
              setToDate(e.target.value);
            }}
          />
        </>
      }
    >
      {(rangeError || error) && (
        <p role="alert" className="form-error">
          {rangeError ?? error}
        </p>
      )}

      {loading && !report && !rangeError && <div className="state-view">Loading…</div>}

      {report && (
        <MetricTiles
          metrics={[
            { label: "Items issued", value: report.totalIssued.toLocaleString() },
            { label: "Cost of items issued", value: money(report.totalCost), note: "at current unit prices" },
            { label: "Released unused", value: report.totalReleased.toLocaleString(), note: "rejected or cancelled" },
            { label: "Restocked", value: report.totalStockedIn.toLocaleString() },
          ]}
        />
      )}

      {report && (
        <div className="split-wide" style={{ gridTemplateColumns: "1.6fr 1fr" }}>
          <Tile label="Usage by item">
            {rows.length === 0 ? (
              <div className="state-view">No consumable moved in this range.</div>
            ) : (
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
                          <button type="button" onClick={() => toggleSort("issued")}>
                            Issued {sortMark("issued")}
                          </button>
                        </th>
                        <th>
                          <button type="button" onClick={() => toggleSort("cost")}>
                            Cost {sortMark("cost")}
                          </button>
                        </th>
                        <th>
                          <button type="button" onClick={() => toggleSort("reserved")}>
                            Reserved {sortMark("reserved")}
                          </button>
                        </th>
                        <th>
                          <button type="button" onClick={() => toggleSort("released")}>
                            Released {sortMark("released")}
                          </button>
                        </th>
                        <th>Held now</th>
                        <th>In stock</th>
                      </tr>
                    </thead>
                    <tbody>
                      {rows.map((r) => (
                        <tr key={r.consumableId}>
                          <td>
                            <b>{r.name}</b>
                            {r.isLowStock && (
                              <>
                                {" "}
                                <Tag tone="outline">Low</Tag>
                              </>
                            )}
                          </td>
                          <td>{r.issued}</td>
                          <td>{money(r.cost)}</td>
                          <td>{r.reserved}</td>
                          <td>{r.released}</td>
                          <td>{r.reservedNow}</td>
                          <td>{r.stockQuantity}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <Pagination
                  showing={`Showing ${firstRow}–${firstRow + rows.length - 1} of ${report.byItem.totalItems}`}
                  disablePrevious={report.byItem.page <= 1}
                  disableNext={report.byItem.page >= report.byItem.totalPages}
                  onPrevious={() => setPage((p) => p - 1)}
                  onNext={() => setPage((p) => p + 1)}
                />
              </>
            )}
          </Tile>

          <Tile label="Low stock now">
            {report.lowStock.length === 0 ? (
              <div className="state-view">Every item is above its reorder level.</div>
            ) : (
              <table className="table">
                <thead>
                  <tr>
                    <th>Item</th>
                    <th>In stock</th>
                    <th>Reorder at</th>
                  </tr>
                </thead>
                <tbody>
                  {report.lowStock.map((l) => (
                    <tr key={l.consumableId}>
                      <td>{l.name}</td>
                      <td>{l.stockQuantity}</td>
                      <td>{l.minStockLevel}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </Tile>
        </div>
      )}
    </Screen>
  );
}
