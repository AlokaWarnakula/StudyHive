import { Link } from "react-router-dom";
import { Screen } from "../components/AppShell";
import { FixtureNotice, KeyValue, Meter, MetricTiles, NotBuiltYet, TagOf, Tile } from "../components/ui";
import { useFixture } from "../dev/useFixture";

const TODAY = new Date().toLocaleDateString(undefined, {
  weekday: "long",
  day: "numeric",
  month: "long",
  year: "numeric",
});

/**
 * W-02 · Dashboard — the landing screen for every staff role.
 *
 * Its four counts and two panels come from S1-S4 read endpoints that do not all exist yet, so the
 * whole screen reads from the development fixture set and says so.
 */
export function DashboardPage() {
  const fixture = useFixture((f) => f.dashboard);

  if (!fixture.enabled) {
    return (
      <Screen title="Dashboard" crumb={TODAY}>
        <NotBuiltYet owner="S1–S4 read" what="The dashboard summary" />
      </Screen>
    );
  }

  const d = fixture.data;

  return (
    <Screen title="Dashboard" crumb={TODAY}>
      <FixtureNotice owner="S4" what="Dashboard counts" />

      <MetricTiles metrics={d.metrics} />

      <div className="split-wide">
        <Tile label="Next in the approval queue" action={<Link to="/approvals">Open queue</Link>}>
          <div className="table-scroll">
            <table className="table">
              <thead>
                <tr>
                  <th>Student</th>
                  <th>Purpose</th>
                  <th>Room · time</th>
                  <th>Total</th>
                  <th>Waiting</th>
                </tr>
              </thead>
              <tbody>
                {d.queue.map((row) => (
                  <tr key={row.student}>
                    <td>{row.student}</td>
                    <td>{row.purpose}</td>
                    <td>{row.slot}</td>
                    <td>{row.total}</td>
                    <td>{row.waiting}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Tile>

        <Tile label="Needs attention">
          {d.attention.map((a) => (
            <KeyValue key={a.text} label={a.text} rule>
              <TagOf tag={a.tag} />
            </KeyValue>
          ))}
          <KeyValue label="Department budget used">{`${d.budgetUsedPercent}%`}</KeyValue>
          <Meter percent={d.budgetUsedPercent} height={8} />
        </Tile>
      </div>
    </Screen>
  );
}
