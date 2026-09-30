import { Link } from "react-router-dom";
import { getBookingsReport, listApprovals, listAuditLogs, listWorkflowExecutions } from "../api/approvals";
import { listLowStock } from "../api/consumables";
import { Screen } from "../components/AppShell";
import { KeyValue, Meter, MetricTiles, Tag, Tile, type Metric } from "../components/ui";
import { formatDateTime, formatMoney, useLoad } from "./approvals/s4";
import { useAuthStore, type StaffRole } from "../store/authStore";

const TODAY = new Date().toLocaleDateString(undefined, {
  weekday: "long",
  day: "numeric",
  month: "long",
  year: "numeric",
});

/** A count that could not be loaded shows a dash rather than a misleading zero. */
function count(value: number | null | undefined): string {
  return value == null ? "—" : String(value);
}

/**
 * Loads only what the role may read — the API answers 403 otherwise: approvals and workflow runs
 * are Librarian-only, the audit log is Admin-only, and the store's low-stock list is shown to the
 * store officer, whose low-stock screen it links to. Each call fails on its
 * own, so one unavailable panel never blanks the rest.
 */
async function loadDashboard(token: string, role: StaffRole) {
  const optional = <T,>(promise: Promise<T>) => promise.catch(() => null);
  const librarian = role === "Librarian";
  const [pending, failed, bookings, audit, lowStock] = await Promise.all([
    librarian ? optional(listApprovals(token, { status: "Pending", pageSize: 5, sortBy: "createdAt", sortDir: "asc" })) : null,
    librarian ? optional(listWorkflowExecutions(token, { status: "Failed", pageSize: 1 })) : null,
    role !== "StoreOfficer" ? optional(getBookingsReport(token)) : null,
    role === "Admin" ? optional(listAuditLogs(token, { pageSize: 5 })) : null,
    role === "StoreOfficer" ? optional(listLowStock(token)) : null,
  ]);
  return { pending, failed, bookings, audit, lowStock };
}

/**
 * W-02 · Dashboard — the landing screen for every staff role, with live counts for what that role
 * works on: the approval queue and failed runs for a librarian, the audit trail for an admin, low
 * stock for the store. Booking figures cover the last 30 days.
 */
export function DashboardPage() {
  const token = useAuthStore((s) => s.accessToken);
  const role = useAuthStore((s) => s.user?.role);
  const loaded = useLoad(
    () => (token && role ? loadDashboard(token, role) : null),
    `${role}`,
    "Failed to load the dashboard.",
  );
  const d = loaded.data;

  const metrics: Metric[] = [];
  if (d) {
    if (role === "Librarian") {
      metrics.push(
        { label: "Waiting for approval", value: count(d.pending?.totalItems), highlight: (d.pending?.totalItems ?? 0) > 0 },
        { label: "Failed workflow runs", value: count(d.failed?.totalItems) },
      );
    }
    if (role !== "StoreOfficer") {
      metrics.push(
        { label: "Requests · 30 days", value: count(d.bookings?.totalRequests) },
        { label: "Approved spend · 30 days", value: d.bookings ? formatMoney(d.bookings.spend.totalSpend) : "—" },
      );
    }
    if (role === "StoreOfficer") {
      metrics.push({ label: "Items low on stock", value: count(d.lowStock?.length), highlight: (d.lowStock?.length ?? 0) > 0 });
    }
  }

  const spendPercent =
    d?.bookings && d.bookings.spend.totalBudget > 0 ? (d.bookings.spend.totalSpend / d.bookings.spend.totalBudget) * 100 : 0;

  return (
    <Screen title="Dashboard" crumb={TODAY}>
      {loaded.error && <p role="alert" className="form-error">{loaded.error}</p>}
      {loaded.loading && !d && <div className="state-view">Loading…</div>}

      {d && (
        <>
          <MetricTiles metrics={metrics} columns={metrics.length === 3 ? 3 : 4} />

          <div className="split-wide">
            {role === "Librarian" && (
              <Tile label="Next in the approval queue" action={<Link to="/approvals">Open queue</Link>}>
                {!d.pending ? (
                  <div className="state-view">The approval queue could not be loaded.</div>
                ) : d.pending.items.length === 0 ? (
                  <div className="state-view">Nothing is waiting for a decision.</div>
                ) : (
                  <div className="table-scroll">
                    <table className="table">
                      <thead>
                        <tr>
                          <th>Objective</th>
                          <th>People</th>
                          <th>Total</th>
                          <th>Quoted</th>
                        </tr>
                      </thead>
                      <tbody>
                        {d.pending.items.map((row) => (
                          <tr key={row.quotationId}>
                            <td>
                              <Link to={`/approvals/${row.quotationId}`}>{row.objective}</Link>
                            </td>
                            <td>{row.groupSize}</td>
                            <td>{formatMoney(row.totalAmount, row.currency)}</td>
                            <td>{formatDateTime(row.createdAt)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </Tile>
            )}

            {role === "Admin" && (
              <Tile label="Latest audit entries" action={<Link to="/audit-log">Open audit log</Link>}>
                {!d.audit ? (
                  <div className="state-view">The audit log could not be loaded.</div>
                ) : d.audit.items.length === 0 ? (
                  <div className="state-view">No audit entries yet.</div>
                ) : (
                  d.audit.items.map((row) => (
                    <KeyValue key={row.id} label={`${row.action} · ${row.entityType}`} rule>
                      <span className="fnote">{formatDateTime(row.createdAt)}</span>
                    </KeyValue>
                  ))
                )}
              </Tile>
            )}

            {role === "StoreOfficer" && (
              <Tile label="Low on stock" action={<Link to="/consumables/low-stock">Open low stock</Link>}>
                {!d.lowStock ? (
                  <div className="state-view">Low stock could not be loaded.</div>
                ) : d.lowStock.length === 0 ? (
                  <div className="state-view">Everything is above its minimum level.</div>
                ) : (
                  d.lowStock.slice(0, 6).map((c) => (
                    <KeyValue key={c.id} label={c.name} rule>
                      <Tag tone="outline">{`${c.availableQuantity} ${c.unit} left`}</Tag>
                    </KeyValue>
                  ))
                )}
              </Tile>
            )}

            {role !== "StoreOfficer" && (
              <Tile label="Spend against budget · 30 days">
                {!d.bookings ? (
                  <div className="state-view">The bookings report could not be loaded.</div>
                ) : (
                  <>
                    <KeyValue label="Approved quotations">{d.bookings.spend.approvedQuotations}</KeyValue>
                    <KeyValue label="Approved spend">{formatMoney(d.bookings.spend.totalSpend)}</KeyValue>
                    <Meter percent={Math.min(spendPercent, 100)} height={8} />
                    <KeyValue label="Budgets quoted against">{formatMoney(d.bookings.spend.totalBudget)}</KeyValue>
                    <KeyValue label="Over budget">{d.bookings.spend.overBudget}</KeyValue>
                  </>
                )}
              </Tile>
            )}
          </div>
        </>
      )}
    </Screen>
  );
}
