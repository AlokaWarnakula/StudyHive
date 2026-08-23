import { NavLink } from "react-router-dom";
import { Screen } from "../../components/AppShell";
import { Bars, FixtureNotice, KeyValue, Meter, NotBuiltYet, Tile } from "../../components/ui";
import { useFixture } from "../../dev/useFixture";

/**
 * The tab strip shared by the three report screens (W-09, W-18, W-24). The reference draws these
 * as tags: the active one filled, the others outlined.
 */
export function ReportTabs() {
  const tabs = [
    { to: "/reports", label: "Bookings by status", end: true },
    { to: "/reports/rooms", label: "Room use" },
    { to: "/reports/consumables", label: "Consumable use" },
  ];
  return (
    <div className="bar">
      {tabs.map((t) => (
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

/**
 * W-09 · Reports — the four owned reports in one place, with a date range and export.
 */
export function ReportsPage() {
  const fixture = useFixture((f) => ({ report: f.reports, month: f.filters.reportMonth }));

  if (!fixture.enabled) {
    return (
      <Screen title="Reports">
        <ReportTabs />
        <NotBuiltYet owner="S1–S4 reporting" what="The bookings report" />
      </Screen>
    );
  }

  const r = fixture.data.report;

  return (
    <Screen
      title="Reports"
      showUser={false}
      actions={
        <>
          <input className="input" style={{ width: 180 }} defaultValue={fixture.data.month} aria-label="Report month" />
          <button type="button" className="btn btn-secondary">
            Export PDF
          </button>
        </>
      }
    >
      <FixtureNotice owner="S4" what="Report figures" />

      <ReportTabs />

      <div className="split-wide" style={{ gridTemplateColumns: "1.3fr 1fr" }}>
        <Tile label="Bookings by status · this month">
          <Bars values={r.weeklyBars} peakIndex={r.weeklyPeakIndex} labels={["W1", "", "W2", "", "W3", "", "W4", ""]} />
          <hr className="hr" />
          <div className="table-scroll">
            <table className="table">
              <thead>
                <tr>
                  <th>Status</th>
                  <th>Count</th>
                  <th>Share</th>
                  <th>Change</th>
                </tr>
              </thead>
              <tbody>
                {r.byStatus.map((s) => (
                  <tr key={s.status}>
                    <td>{s.status}</td>
                    <td>{s.count}</td>
                    <td>{s.share}</td>
                    <td>{s.change}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Tile>

        <div className="stack">
          <Tile label="Busiest room">
            <span className="big">{r.busiestRoom.value}</span>
            <span className="fnote">{r.busiestRoom.note}</span>
          </Tile>
          <Tile label="Most used item">
            <span className="big">{r.mostUsedItem.value}</span>
            <span className="fnote">{r.mostUsedItem.note}</span>
          </Tile>
          <Tile label="Spend against budget">
            <KeyValue label="Committed">{r.committed}</KeyValue>
            <Meter percent={r.budgetPercent} />
            <KeyValue label="Budget">{r.budget}</KeyValue>
            <KeyValue label="Left">{r.left}</KeyValue>
          </Tile>
        </div>
      </div>
    </Screen>
  );
}
