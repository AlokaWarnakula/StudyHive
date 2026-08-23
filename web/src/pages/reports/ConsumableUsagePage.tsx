import { Screen } from "../../components/AppShell";
import { Bars, FixtureNotice, KeyValue, MetricTiles, NotBuiltYet, Tile } from "../../components/ui";
import { useFixture } from "../../dev/useFixture";
import { ReportTabs } from "./ReportsPage";

/**
 * W-24 · Consumable usage report — S3's own report: usage per item, cost, wastage and how often
 * an item ran out.
 */
export function ConsumableUsagePage() {
  const fixture = useFixture((f) => ({ report: f.consumableUsage, month: f.filters.reportMonth }));

  if (!fixture.enabled) {
    return (
      <Screen title="Consumable usage">
        <ReportTabs />
        <NotBuiltYet owner="S3 store" what="The consumable usage report" />
      </Screen>
    );
  }

  const c = fixture.data.report;

  return (
    <Screen
      title="Consumable usage"
      showUser={false}
      actions={
        <>
          <input className="input" style={{ width: 170 }} defaultValue={fixture.data.month} aria-label="Report month" />
          <button type="button" className="btn btn-secondary">
            Export CSV
          </button>
        </>
      }
    >
      <FixtureNotice owner="S3" what="Consumable usage figures" />

      <ReportTabs />

      <MetricTiles metrics={c.metrics} />

      <div className="split-wide" style={{ gridTemplateColumns: "1.2fr 1fr" }}>
        <Tile label="Usage by item">
          <div className="table-scroll">
            <table className="table">
              <thead>
                <tr>
                  <th>Item</th>
                  <th>Issued</th>
                  <th>Cost</th>
                  <th>Reserved now</th>
                  <th>Out of stock</th>
                </tr>
              </thead>
              <tbody>
                {c.byItem.map((r) => (
                  <tr key={r.item}>
                    <td>{r.item}</td>
                    <td>{r.issued}</td>
                    <td>{r.cost}</td>
                    <td>{r.reservedNow}</td>
                    <td>{r.outOfStock}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Tile>

        <Tile label="Items issued per week">
          <Bars values={c.perWeek} peakIndex={c.perWeekPeakIndex} labels={["W1", "W2", "W3", "W4"]} />
          <hr className="hr" />
          <KeyValue label="Busiest day">{c.busiestDay}</KeyValue>
          <KeyValue label="Average per booking">{c.averagePerBooking}</KeyValue>
        </Tile>
      </div>
    </Screen>
  );
}
