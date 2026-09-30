import { useState } from "react";
import { NavLink } from "react-router-dom";
import { getBookingsReport } from "../../api/approvals";
import { Screen } from "../../components/AppShell";
import { Bars, KeyValue, Meter, MetricTiles, Tile } from "../../components/ui";
import { useAuthStore, type StaffRole } from "../../store/authStore";
import { formatMoney, humanize, useLoad } from "../approvals/s4";

/**
 * The tab strip shared by the report screens (W-09, W-18). The reference draws these as tags: the
 * active one filled, the others outlined. Each tab carries the roles its API serves, so an Admin
 * never gets a tab whose report would answer 403 (room usage is Librarian-only). W-24 consumable
 * usage is StoreOfficer-only and lives under the Store group instead.
 */
export function ReportTabs() {
  const role = useAuthStore((s) => s.user?.role);
  const tabs: { to: string; label: string; end?: boolean; allow: StaffRole[] }[] = [
    { to: "/reports", label: "Bookings by status", end: true, allow: ["Librarian", "Admin"] },
    { to: "/reports/rooms", label: "Room use", allow: ["Librarian"] },
  ];
  return (
    <div className="bar">
      {tabs
        .filter((t) => role && t.allow.includes(role))
        .map((t) => (
          <NavLink
            key={t.to}
            to={t.to}
            end={t.end}
            className={({ isActive }) => (isActive ? "tag tag-accent" : "tag tag-outline")}
            style={{ textDecoration: "none" }}
          >
            {t.label}
          </NavLink>
        ))}
    </div>
  );
}

/** The first instant of a yyyy-MM month and of the month after it, in UTC. */
function monthRange(month: string): { from: string; to: string } {
  const [year, monthNumber] = month.split("-").map(Number);
  return {
    from: new Date(Date.UTC(year, monthNumber - 1, 1)).toISOString(),
    to: new Date(Date.UTC(year, monthNumber, 1)).toISOString(),
  };
}

/**
 * W-09 · Bookings report — GET /api/reports/bookings (Librarian or Admin): requests by status,
 * requests and approvals per week, and approved spend against the budgets quoted.
 */
export function ReportsPage() {
  const token = useAuthStore((s) => s.accessToken);
  const [month, setMonth] = useState(() => new Date().toISOString().slice(0, 7));
  const { from, to } = monthRange(month);
  const report = useLoad(() => (token ? getBookingsReport(token, from, to) : null), month, "Failed to load the bookings report.");
  const r = report.data;

  const weekly = r?.byWeek.map((w) => w.requests) ?? [];
  const weeklyMax = Math.max(1, ...weekly);
  const spendPercent = r && r.spend.totalBudget > 0 ? (r.spend.totalSpend / r.spend.totalBudget) * 100 : 0;

  return (
    <Screen
      title="Reports"
      showUser={false}
      actions={
        <input
          className="input"
          style={{ width: 170 }}
          type="month"
          value={month}
          onChange={(e) => e.target.value && setMonth(e.target.value)}
          aria-label="Report month"
        />
      }
    >
      <ReportTabs />

      {report.error && <p role="alert" className="form-error">{report.error}</p>}
      {report.loading && !r && <div className="state-view">Loading…</div>}

      {r && (
        <>
          <MetricTiles
            metrics={[
              { label: "Requests", value: String(r.totalRequests) },
              { label: "Approved quotations", value: String(r.spend.approvedQuotations) },
              { label: "Approved spend", value: formatMoney(r.spend.totalSpend) },
              {
                label: "Over budget",
                value: String(r.spend.overBudget),
                note: `${r.spend.withinBudget} within budget`,
                highlight: r.spend.overBudget > 0,
              },
            ]}
          />

          <div className="split-wide" style={{ gridTemplateColumns: "1.3fr 1fr" }}>
            <Tile label="Requests by week">
              {r.byWeek.length === 0 ? (
                <div className="state-view">No requests in this month.</div>
              ) : (
                <>
                  <Bars
                    values={weekly.map((n) => (n / weeklyMax) * 100)}
                    peakIndex={weekly.indexOf(Math.max(...weekly))}
                    labels={r.byWeek.map((w) => w.weekStart.slice(5))}
                  />
                  <hr className="hr" />
                  <div className="table-scroll">
                    <table className="table">
                      <thead>
                        <tr>
                          <th>Week of</th>
                          <th>Requests</th>
                          <th>Approved</th>
                          <th>Approved spend</th>
                          <th>Their budgets</th>
                        </tr>
                      </thead>
                      <tbody>
                        {r.byWeek.map((w) => (
                          <tr key={w.weekStart}>
                            <td>{w.weekStart}</td>
                            <td>{w.requests}</td>
                            <td>{w.approved}</td>
                            <td>{formatMoney(w.approvedSpend)}</td>
                            <td>{formatMoney(w.approvedBudget)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </>
              )}
            </Tile>

            <div className="stack">
              <Tile label="Bookings by status">
                {r.totalRequests === 0 ? (
                  <div className="state-view">No requests in this month.</div>
                ) : (
                  r.byStatus.filter((s) => s.count > 0).map((s) => (
                    <KeyValue key={s.status} label={humanize(s.status)}>
                      {`${s.count} · ${r.totalRequests ? Math.round((s.count / r.totalRequests) * 100) : 0}%`}
                    </KeyValue>
                  ))
                )}
              </Tile>
              <Tile label="Spend against budget">
                <KeyValue label="Approved spend">{formatMoney(r.spend.totalSpend)}</KeyValue>
                <Meter percent={Math.min(spendPercent, 100)} />
                <KeyValue label="Budgets quoted against">{formatMoney(r.spend.totalBudget)}</KeyValue>
                <KeyValue label="Left">{formatMoney(r.spend.totalBudget - r.spend.totalSpend)}</KeyValue>
              </Tile>
            </div>
          </div>
        </>
      )}
    </Screen>
  );
}
