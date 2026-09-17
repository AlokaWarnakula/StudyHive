import { useEffect, useState } from "react";
import { ApiError } from "../../api/client";
import { createMaintenanceWindow, listMaintenanceWindows, listRooms, type MaintenanceWindow, type PagedResult, type Room } from "../../api/rooms";
import { Screen } from "../../components/AppShell";
import { Field, Pagination, Tag, Tile, Toolbar } from "../../components/ui";
import { useAuthStore } from "../../store/authStore";

const PAGE_SIZE = 20;

/** W-17 · Live maintenance list and scheduling form. Owned by S2. */
export function MaintenancePage() {
  const token = useAuthStore((s) => s.accessToken);
  const [result, setResult] = useState<PagedResult<MaintenanceWindow> | null>(null);
  const [rooms, setRooms] = useState<Room[]>([]);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [roomId, setRoomId] = useState("");
  const [reason, setReason] = useState("");
  const [startsAt, setStartsAt] = useState("");
  const [endsAt, setEndsAt] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [reload, setReload] = useState(0);

  useEffect(() => {
    if (!token) return;
    let cancelled = false;
    listRooms(token, { pageSize: 100, sortBy: "name", sortDir: "asc" }).then((data) => {
      if (cancelled) return;
      const active = data.items.filter((room) => room.isActive);
      setRooms(active); setRoomId((current) => current || active[0]?.id || "");
    }).catch((err) => !cancelled && setError(messageOf(err, "Failed to load rooms.")));
    return () => { cancelled = true; };
  }, [token]);

  useEffect(() => {
    if (!token) return;
    let cancelled = false;
    setLoading(true); setError(null);
    listMaintenanceWindows(token, { page, pageSize: PAGE_SIZE, search: search || undefined, sortBy: "startsAt", sortDir: "desc" })
      .then((data) => !cancelled && setResult(data))
      .catch((err) => !cancelled && setError(messageOf(err, "Failed to load maintenance windows.")))
      .finally(() => !cancelled && setLoading(false));
    return () => { cancelled = true; };
  }, [token, page, search, reload]);

  async function save() {
    if (!token || !roomId || !reason.trim() || !startsAt || !endsAt) { setError("Room, reason, start and end times are required."); return; }
    const start = new Date(startsAt); const end = new Date(endsAt);
    if (end <= start) { setError("End time must be later than start time."); return; }
    setSaving(true); setError(null); setNotice(null);
    try {
      const created = await createMaintenanceWindow(token, { roomId, reason, startsAt: start.toISOString(), endsAt: end.toISOString() });
      setNotice(created.affectedBookings === 0 ? "Maintenance window saved." : `Window saved and affects ${created.affectedBookings} confirmed booking(s).`);
      setReason(""); setStartsAt(""); setEndsAt(""); setPage(1); setReload((value) => value + 1);
    } catch (err) { setError(messageOf(err, "Failed to save the maintenance window.")); }
    finally { setSaving(false); }
  }

  const items = result?.items ?? [];
  const first = result && result.totalItems > 0 ? (result.page - 1) * result.pageSize + 1 : 0;
  return <Screen title="Maintenance" crumb={result ? `${result.totalItems} windows` : undefined}>
    <div className="split-wide"><div className="stack">
      <Toolbar><input className="input" style={{ maxWidth: 280 }} type="search" placeholder="Search room or reason" aria-label="Search room or reason"
        value={search} onChange={(e) => { setPage(1); setSearch(e.target.value); }} /></Toolbar>
      {error && <p role="alert" className="form-error">{error}</p>}
      {loading && !result && <div className="state-view">Loading…</div>}
      {result && items.length === 0 && !loading && <div className="state-view">No maintenance windows match this search.</div>}
      {items.length > 0 && <><div className="table-scroll"><table className="table"><thead><tr><th>Room</th><th>Reason</th><th>From</th><th>To</th><th>Bookings hit</th><th>Status</th></tr></thead><tbody>
        {items.map((window) => { const status = windowStatus(window); return <tr key={window.id}><td><b>{window.roomName}</b></td><td>{window.reason}</td>
          <td>{new Date(window.startsAt).toLocaleString()}</td><td>{new Date(window.endsAt).toLocaleString()}</td><td>{window.affectedBookings}</td>
          <td><Tag tone={status === "Active" ? "outline" : status === "Planned" ? "accent" : "neutral"}>{status}</Tag></td></tr>; })}
      </tbody></table></div><Pagination showing={`Showing ${first}–${first + items.length - 1} of ${result!.totalItems}`}
        disablePrevious={result!.page <= 1} disableNext={result!.page >= result!.totalPages}
        onPrevious={() => setPage((value) => value - 1)} onNext={() => setPage((value) => value + 1)} /></>}
    </div>
    <Tile label="Schedule a window" accented>
      <Field label="Room"><select className="input" aria-label="Room" value={roomId} onChange={(e) => setRoomId(e.target.value)}>{rooms.map((room) => <option key={room.id} value={room.id}>{room.name} — {room.building}, floor {room.floor}</option>)}</select></Field>
      <Field label="Reason"><input className="input" aria-label="Reason" value={reason} onChange={(e) => setReason(e.target.value)} /></Field>
      <div className="k2"><Field label="From"><input className="input" aria-label="From" type="datetime-local" value={startsAt} onChange={(e) => setStartsAt(e.target.value)} /></Field>
        <Field label="To"><input className="input" aria-label="To" type="datetime-local" value={endsAt} onChange={(e) => setEndsAt(e.target.value)} /></Field></div>
      {notice && <div className="notice-accent" role="status">{notice}</div>}
      <button type="button" className="btn btn-primary btn-block" style={{ padding: 11 }} disabled={saving || rooms.length === 0} onClick={save}>{saving ? "Saving…" : "Save window"}</button>
    </Tile></div>
  </Screen>;
}

function windowStatus(window: MaintenanceWindow) {
  const now = Date.now();
  if (new Date(window.endsAt).getTime() <= now) return "Finished";
  if (new Date(window.startsAt).getTime() <= now) return "Active";
  return "Planned";
}
function messageOf(error: unknown, fallback: string) { return error instanceof ApiError ? error.message : fallback; }
