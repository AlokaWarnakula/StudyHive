import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Screen } from "../../components/AppShell";
import { Icon } from "../../components/Icon";
import {
  CheckRow,
  Dialog,
  Field,
  FixtureNotice,
  NotBuiltYet,
  Pagination,
  Select,
  TagOf,
  TextField,
  Toolbar,
} from "../../components/ui";
import { useFixture } from "../../dev/useFixture";

/**
 * W-13 · Rooms, plus the add/edit room dialog.
 * GET /api/rooms · POST/PUT /api/rooms — the dialog validates capacity > 0 and a unique code.
 * Owned by S2.
 */
export function RoomsPage() {
  const fixture = useFixture((f) => ({ rooms: f.rooms, form: f.forms.newRoom, buildings: f.filters.buildings }));
  const navigate = useNavigate();
  const [search, setSearch] = useState("");
  const [dialogOpen, setDialogOpen] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  // Both start null so the reference's prefilled values come from the fixture, not from here.
  const [code, setCode] = useState<string | null>(null);
  const [seats, setSeats] = useState<string | null>(null);

  if (!fixture.enabled) {
    return (
      <Screen title="Rooms">
        <NotBuiltYet owner="S2 rooms" what="The room list" />
      </Screen>
    );
  }

  const { rooms, form, buildings } = fixture.data;
  const codeValue = code ?? form.code;
  const seatsValue = seats ?? form.seats;
  const term = search.trim().toLowerCase();
  const rows = term ? rooms.filter((r) => `${r.code} ${r.location}`.toLowerCase().includes(term)) : rooms;

  function save() {
    // The two rules the reference names on this dialog, enforced before anything is submitted.
    if (Number(seatsValue) <= 0 || Number.isNaN(Number(seatsValue))) {
      setFormError("Seats must be a number greater than 0.");
      return;
    }
    if (rooms.some((r) => r.code.toLowerCase() === codeValue.trim().toLowerCase())) {
      setFormError(`Room code ${codeValue} is already used.`);
      return;
    }
    setFormError(null);
    setDialogOpen(false);
  }

  return (
    <Screen
      title="Rooms"
      crumb={`${rooms.length} rooms · 2 buildings`}
      showUser={false}
      actions={
        <button type="button" className="btn btn-primary" onClick={() => setDialogOpen(true)}>
          <Icon name="plus" size={16} />
          Add room
        </button>
      }
    >
      <FixtureNotice owner="S2" what="Rooms" />

      <Toolbar>
        <input
          className="input"
          style={{ maxWidth: 250 }}
          placeholder="Search name or building"
          aria-label="Search name or building"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <Select label="Capacity" options={["Any", "1–4", "5–10", "10+"]} />
        <Select label="Equipment" options={["Any", "Whiteboard", "Projector", "TV", "Sound"]} />
        <Select label="Status" options={["All", "Available", "Maintenance", "Closed"]} />
      </Toolbar>

      <div className="table-scroll">
        <table className="table">
          <thead>
            <tr>
              <th>Room</th>
              <th>Building · floor</th>
              <th>Seats</th>
              <th>Equipment</th>
              <th>Rate / hour</th>
              <th>Today</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.code} className="row-click" onClick={() => navigate(`/rooms/${r.code}`)}>
                <td>
                  <b>{r.code}</b>
                </td>
                <td>{r.location}</td>
                <td>{r.seats}</td>
                <td>{r.equipment}</td>
                <td>{r.rate}</td>
                <td>{r.today}</td>
                <td>
                  <TagOf tag={r.status} />
                </td>
                <td>
                  <Link to={`/rooms/${r.code}`}>Open</Link>
                </td>
              </tr>
            ))}
            {rows.length === 0 && (
              <tr>
                <td colSpan={8}>
                  <div className="state-view">No room matches “{search}”.</div>
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <Pagination showing={`Showing 1–${rows.length} of 12`} disablePrevious />

      {dialogOpen && (
        <Dialog
          title="Add room"
          width={520}
          onClose={() => setDialogOpen(false)}
          actions={
            <>
              <button type="button" className="btn btn-secondary" onClick={() => setDialogOpen(false)}>
                Cancel
              </button>
              <button type="button" className="btn btn-primary" onClick={save}>
                Save room
              </button>
            </>
          }
        >
          <div className="k2">
            <Field label="Room code">
              <input className="input" value={codeValue} aria-label="Room code" onChange={(e) => setCode(e.target.value)} />
            </Field>
            <Field label="Seats">
              <input className="input" value={seatsValue} aria-label="Seats" onChange={(e) => setSeats(e.target.value)} />
            </Field>
            <Field label="Building">
              <select className="input" aria-label="Building">
                {buildings.map((b) => (
                  <option key={b}>{b}</option>
                ))}
              </select>
            </Field>
            <TextField label="Floor" defaultValue={form.floor} />
            <TextField label="Rate per hour (Rs.)" defaultValue={form.rate} />
            <Field label="Status">
              <select className="input" aria-label="Status">
                <option>Available</option>
                <option>Closed</option>
              </select>
            </Field>
          </div>

          <Field label="Equipment installed">
            <div style={{ display: "flex", gap: 14, flexWrap: "wrap", marginTop: 4 }}>
              <CheckRow label="Whiteboard" defaultChecked />
              <CheckRow label="Projector" defaultChecked />
              <CheckRow label="TV" />
              <CheckRow label="Sound system" />
            </div>
          </Field>

          <TextField label="Notes (optional)" textarea />

          {formError && (
            <p role="alert" className="form-error">
              {formError}
            </p>
          )}
        </Dialog>
      )}
    </Screen>
  );
}
