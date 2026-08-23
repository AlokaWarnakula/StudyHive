import { Screen } from "../../components/AppShell";
import { Bars, FixtureNotice, KeyValue, Meter, MetricTiles, NotBuiltYet, Tile } from "../../components/ui";
import { useFixture } from "../../dev/useFixture";
import { ReportTabs } from "./ReportsPage";

/**
 * W-18 · Room utilisation report — S2's own report: utilisation by room, peak hours, no-shows.
 */
export function RoomUtilisationPage() {
  const fixture = useFixture((f) => ({ report: f.roomUsage, month: f.filters.reportMonth }));

  if (!fixture.enabled) {
    return (
      <Screen title="Room utilisation">
        <ReportTabs />
        <NotBuiltYet owner="S2 rooms" what="The room utilisation report" />
      </Screen>
    );
  }

  const r = fixture.data.report;

  return (
    <Screen
      title="Room utilisation"
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
      <FixtureNotice owner="S2" what="Room utilisation figures" />

      <ReportTabs />

      <MetricTiles metrics={r.metrics} />

      <div className="k2">
        <Tile label="Utilisation by room">
          <div style={{ display: "flex", flexDirection: "column", gap: 10, marginTop: 6 }}>
            {r.byRoom.map((row) => (
              <div key={row.room}>
                <KeyValue label={row.room}>{`${row.percent}%`}</KeyValue>
                <Meter percent={row.percent} height={9} />
              </div>
            ))}
          </div>
        </Tile>

        <Tile label="Bookings by hour of day">
          <Bars values={r.byHour} peakIndex={r.byHourPeakIndex} labels={r.hourLabels} />
        </Tile>
      </div>
    </Screen>
  );
}
