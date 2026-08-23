import { useState } from "react";
import { Screen } from "../../components/AppShell";
import { Icon } from "../../components/Icon";
import { FixtureNotice, MetricTiles, NotBuiltYet, Pagination, Select, TagOf, Toolbar } from "../../components/ui";
import { useFixture } from "../../dev/useFixture";

/**
 * W-16 · Equipment — GET /api/equipment: condition, assignment to a room and the repair flag.
 * Owned by S2.
 */
export function EquipmentPage() {
  const fixture = useFixture((f) => ({ equipment: f.equipment, filters: f.filters }));
  const [search, setSearch] = useState("");

  if (!fixture.enabled) {
    return (
      <Screen title="Equipment">
        <NotBuiltYet owner="S2 rooms" what="The equipment register" />
      </Screen>
    );
  }

  const e = fixture.data.equipment;
  const filters = fixture.data.filters;
  const term = search.trim().toLowerCase();
  const rows = term ? e.rows.filter((r) => `${r.item} ${r.serial}`.toLowerCase().includes(term)) : e.rows;

  return (
    <Screen
      title="Equipment"
      crumb={`${e.total} items · 3 under repair`}
      showUser={false}
      actions={
        <button type="button" className="btn btn-primary">
          <Icon name="plus" size={16} />
          Add equipment
        </button>
      }
    >
      <FixtureNotice owner="S2" what="The equipment register" />

      <MetricTiles metrics={e.metrics} />

      <Toolbar>
        <input
          className="input"
          style={{ maxWidth: 250 }}
          placeholder="Search item or serial"
          aria-label="Search item or serial"
          value={search}
          onChange={(ev) => setSearch(ev.target.value)}
        />
        <Select label="Type" options={filters.equipmentTypes} />
        <Select label="Room" options={filters.equipmentRooms} />
        <Select label="Condition" options={["All", "Working", "Under repair", "Retired"]} />
      </Toolbar>

      <div className="table-scroll">
        <table className="table">
          <thead>
            <tr>
              <th>Item</th>
              <th>Type</th>
              <th>Serial</th>
              <th>Room</th>
              <th>Condition</th>
              <th>Last checked</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.serial}>
                <td>{r.item}</td>
                <td>{r.type}</td>
                <td>{r.serial}</td>
                <td>{r.room}</td>
                <td>
                  <TagOf tag={r.condition} />
                </td>
                <td>{r.lastChecked}</td>
                <td>
                  <button type="button" className="btn btn-ghost">
                    {r.action}
                  </button>
                </td>
              </tr>
            ))}
            {rows.length === 0 && (
              <tr>
                <td colSpan={7}>
                  <div className="state-view">No equipment matches “{search}”.</div>
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <Pagination showing={`Showing 1–${rows.length} of ${e.total}`} disablePrevious />
    </Screen>
  );
}
