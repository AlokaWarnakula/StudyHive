import { useNavigate, useParams } from "react-router-dom";
import { Screen } from "../../components/AppShell";
import { FixtureNotice, KeyValue, NotBuiltYet, Placeholder, TagOf, Tile } from "../../components/ui";
import { useFixture } from "../../dev/useFixture";

/**
 * W-14 · Room detail & equipment — GET /api/rooms/{id}: equipment condition, upcoming bookings
 * and maintenance history. Owned by S2.
 */
export function RoomDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const fixture = useFixture((f) => f.roomDetail);

  if (!fixture.enabled) {
    return (
      <Screen title={`Room ${id ?? ""}`} crumb="Rooms" onBack={() => navigate("/rooms")}>
        <NotBuiltYet owner="S2 rooms" what="Room detail" />
      </Screen>
    );
  }

  const r = fixture.data;

  return (
    <Screen
      title={`Room ${id ?? r.code}`}
      crumb={`Rooms / ${id ?? r.code}`}
      onBack={() => navigate("/rooms")}
      showUser={false}
      actions={
        <>
          <button type="button" className="btn btn-secondary" onClick={() => navigate("/maintenance")}>
            Schedule maintenance
          </button>
          <button type="button" className="btn btn-secondary">
            Edit room
          </button>
          <button type="button" className="btn btn-primary" onClick={() => navigate("/rooms/calendar")}>
            View calendar
          </button>
        </>
      }
    >
      <FixtureNotice owner="S2" what="Room detail, equipment and upcoming bookings" />

      <div className="split-wide" style={{ gridTemplateColumns: "1fr 1.3fr" }}>
        <div className="stack">
          <Placeholder label="room photo" height={190} />
          <Tile>
            {r.facts.map((f) => (
              <KeyValue key={f.label} label={f.label}>
                {f.value}
              </KeyValue>
            ))}
            <KeyValue label="Status">
              <TagOf tag={r.status} />
            </KeyValue>
          </Tile>
        </div>

        <div className="stack">
          <Tile
            label="Installed equipment"
            action={
              <button type="button" className="btn btn-secondary">
                Add equipment
              </button>
            }
          >
            <div className="table-scroll">
              <table className="table">
                <thead>
                  <tr>
                    <th>Item</th>
                    <th>Serial</th>
                    <th>Condition</th>
                    <th>Last checked</th>
                  </tr>
                </thead>
                <tbody>
                  {r.equipment.map((e) => (
                    <tr key={e.serial}>
                      <td>{e.item}</td>
                      <td>{e.serial}</td>
                      <td>
                        <TagOf tag={e.condition} />
                      </td>
                      <td>{e.lastChecked}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Tile>

          <Tile label="Upcoming bookings">
            <div className="table-scroll">
              <table className="table">
                <thead>
                  <tr>
                    <th>When</th>
                    <th>Student</th>
                    <th>People</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {r.upcoming.map((b) => (
                    <tr key={`${b.when}-${b.student}`}>
                      <td>{b.when}</td>
                      <td>{b.student}</td>
                      <td>{b.people}</td>
                      <td>
                        <TagOf tag={b.status} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Tile>
        </div>
      </div>
    </Screen>
  );
}
