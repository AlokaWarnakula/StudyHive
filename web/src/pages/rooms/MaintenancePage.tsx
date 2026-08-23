import { useState } from "react";
import { Screen } from "../../components/AppShell";
import {
  CheckRow,
  Field,
  FixtureNotice,
  NotBuiltYet,
  Pagination,
  Select,
  TagOf,
  TextField,
  Tile,
  Toolbar,
} from "../../components/ui";
import { useFixture } from "../../dev/useFixture";

/**
 * W-17 · Maintenance windows — POST /api/maintenance-windows. A saved window blocks the room from
 * availability search, so the form warns when approved bookings fall inside it. Owned by S2.
 */
export function MaintenancePage() {
  const fixture = useFixture((f) => ({ maintenance: f.maintenance, filters: f.filters, form: f.forms.maintenanceWindow }));
  const [search, setSearch] = useState("");

  if (!fixture.enabled) {
    return (
      <Screen title="Maintenance">
        <NotBuiltYet owner="S2 rooms" what="Maintenance windows" />
      </Screen>
    );
  }

  const m = fixture.data.maintenance;
  const filters = fixture.data.filters;
  const form = fixture.data.form;
  const term = search.trim().toLowerCase();
  const rows = term ? m.rows.filter((r) => `${r.room} ${r.reason}`.toLowerCase().includes(term)) : m.rows;

  return (
    <Screen title="Maintenance" crumb="2 active · 3 planned">
      <FixtureNotice owner="S2" what="Maintenance windows" />

      <div className="split-wide">
        <div className="stack">
          <Toolbar>
            <input
              className="input"
              style={{ maxWidth: 230 }}
              placeholder="Search room or reason"
              aria-label="Search room or reason"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
            <Select label="Room" options={filters.rooms} width={160} />
            <Select label="Status" options={["All", "Active", "Planned", "Finished"]} width={160} />
          </Toolbar>

          <div className="table-scroll">
            <table className="table">
              <thead>
                <tr>
                  <th>Room</th>
                  <th>Reason</th>
                  <th>From</th>
                  <th>To</th>
                  <th>Bookings hit</th>
                  <th>Status</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {rows.map((r) => (
                  <tr key={`${r.room}-${r.from}`}>
                    <td>
                      <b>{r.room}</b>
                    </td>
                    <td>{r.reason}</td>
                    <td>{r.from}</td>
                    <td>{r.to}</td>
                    <td>{r.bookingsHit}</td>
                    <td>
                      <TagOf tag={r.status} />
                    </td>
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
                      <div className="state-view">No window matches “{search}”.</div>
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          <Pagination showing={`Showing 1–${rows.length} of ${m.total}`} disablePrevious />
        </div>

        <Tile label="Schedule a window" accented>
          <Field label="Room">
            <select className="input" aria-label="Room">
              {form.rooms.map((r) => (
                <option key={r}>{r}</option>
              ))}
            </select>
          </Field>
          <TextField label="Reason" defaultValue={form.reason} />
          <div className="k2">
            <TextField label="From" defaultValue={form.from} />
            <TextField label="To" defaultValue={form.to} />
          </div>
          <TextField label="Notes for staff" defaultValue={form.notes} textarea />

          <div className="notice-accent" role="status">
            {m.clashWarning}
          </div>

          <CheckRow label="Email affected students" defaultChecked />
          <button type="button" className="btn btn-primary btn-block" style={{ padding: 11 }}>
            Save window
          </button>
        </Tile>
      </div>
    </Screen>
  );
}
