import { useEffect, useState } from "react";
import { colomboStamp } from "../../utils/colomboTime";
import { ApiError } from "../../api/client";
import {
  createMaintenanceWindow, deleteMaintenanceWindow, listMaintenanceWindows, listRooms, updateMaintenanceWindow,
  type MaintenanceWindow, type OverlappingBooking, type PagedResult, type Room,
} from "../../api/rooms";
import { can } from "../../auth/permissions";
import { Screen } from "../../components/AppShell";
import { Dialog, Field, Pagination, Tag, Tile, Toolbar } from "../../components/ui";
import { useAuthStore } from "../../store/authStore";

const PAGE_SIZE = 20;

/** The request the librarian is asked to confirm after a 409 overlap. */
interface PendingOverlap {
  bookings: OverlappingBooking[];
  confirm: () => Promise<void>;
}

/**
 * W-17 · Live maintenance list and scheduling form. Owned by S2.
 *
 * AUDIT CW-06: future windows can be edited and cancelled; a window over Confirmed bookings is
 * refused with the bookings listed, and "Schedule anyway" sends it again with force=true, which
 * emails each affected student. CW-05: only a Librarian schedules (Admin reads the list).
 */
export function MaintenancePage() {
  const token = useAuthStore((s) => s.accessToken);
  const role = useAuthStore((s) => s.user?.role);
  const canEdit = can(role, "maintenance.edit");
  const [result, setResult] = useState<PagedResult<MaintenanceWindow> | null>(null);
  const [rooms, setRooms] = useState<Room[]>([]);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<MaintenanceWindow | null>(null);
  const [roomId, setRoomId] = useState("");
  const [reason, setReason] = useState("");
  const [startsAt, setStartsAt] = useState("");
  const [endsAt, setEndsAt] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [overlap, setOverlap] = useState<PendingOverlap | null>(null);
  const [cancelling, setCancelling] = useState<MaintenanceWindow | null>(null);
  const [reload, setReload] = useState(0);

  useEffect(() => {
    if (!token || !canEdit) return;
    let cancelled = false;
    listRooms(token, { pageSize: 100, sortBy: "name", sortDir: "asc" }).then((data) => {
      if (cancelled) return;
      const active = data.items.filter((room) => room.isActive);
      setRooms(active); setRoomId((current) => current || active[0]?.id || "");
    }).catch((err) => !cancelled && setError(messageOf(err, "Failed to load rooms.")));
    return () => { cancelled = true; };
  }, [token, canEdit]);

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

  function resetForm() {
    setEditing(null); setReason(""); setStartsAt(""); setEndsAt(""); setFormError(null);
  }

  function startEdit(window: MaintenanceWindow) {
    setEditing(window);
    setRoomId(window.roomId);
    setReason(window.reason);
    setStartsAt(toLocalInput(window.startsAt));
    setEndsAt(toLocalInput(window.endsAt));
    setFormError(null); setNotice(null);
  }

  async function submit(force: boolean) {
    if (!token) return;
    const start = new Date(startsAt); const end = new Date(endsAt);
    const body = { reason, startsAt: start.toISOString(), endsAt: end.toISOString() };
    const saved = editing
      ? await updateMaintenanceWindow(token, editing.id, body, force)
      : await createMaintenanceWindow(token, { ...body, roomId }, force);
    setNotice(
      saved.affectedBookings === 0
        ? editing ? "Maintenance window updated." : "Maintenance window saved."
        : `Window saved over ${saved.affectedBookings} confirmed booking(s); the students are emailed.`,
    );
    resetForm(); setPage(1); setReload((value) => value + 1);
  }

  async function save() {
    if (!token || !roomId || !reason.trim() || !startsAt || !endsAt) { setFormError("Room, reason, start and end times are required."); return; }
    if (new Date(endsAt) <= new Date(startsAt)) { setFormError("End time must be later than start time."); return; }
    setSaving(true); setFormError(null); setNotice(null);
    try {
      await submit(false);
    } catch (err) {
      const bookings = overlappingBookings(err);
      if (bookings) setOverlap({ bookings, confirm: () => submit(true) });
      else setFormError(messageOf(err, "Failed to save the maintenance window."));
    } finally { setSaving(false); }
  }

  async function confirmOverlap() {
    if (!overlap) return;
    setSaving(true);
    try { await overlap.confirm(); setOverlap(null); }
    catch (err) { setOverlap(null); setFormError(messageOf(err, "Failed to save the maintenance window.")); }
    finally { setSaving(false); }
  }

  async function confirmCancel() {
    if (!token || !cancelling) return;
    setSaving(true);
    try {
      await deleteMaintenanceWindow(token, cancelling.id);
      setNotice("Maintenance window cancelled.");
      if (editing?.id === cancelling.id) resetForm();
      setReload((value) => value + 1);
    } catch (err) { setError(messageOf(err, "Failed to cancel the maintenance window.")); }
    finally { setCancelling(null); setSaving(false); }
  }

  const items = result?.items ?? [];
  const first = result && result.totalItems > 0 ? (result.page - 1) * result.pageSize + 1 : 0;
  return <Screen title="Maintenance" crumb={result ? `${result.totalItems} windows` : undefined}>
    <div className={canEdit ? "split-wide" : undefined}><div className="stack">
      <Toolbar><input className="input" style={{ maxWidth: 280 }} type="search" placeholder="Search room or reason" aria-label="Search room or reason"
        value={search} onChange={(e) => { setPage(1); setSearch(e.target.value); }} /></Toolbar>
      {error && <p role="alert" className="form-error">{error}</p>}
      {loading && !result && <div className="state-view">Loading…</div>}
      {result && items.length === 0 && !loading && <div className="state-view">No maintenance windows match this search.</div>}
      {items.length > 0 && <><div className="table-scroll"><table className="table"><thead><tr><th>Room</th><th>Reason</th><th>From</th><th>To</th><th>Bookings hit</th><th>Status</th>{canEdit && <th />}</tr></thead><tbody>
        {items.map((window) => { const status = windowStatus(window); return <tr key={window.id}><td><b>{window.roomName}</b></td><td>{window.reason}</td>
          <td>{colomboStamp(window.startsAt)}</td><td>{colomboStamp(window.endsAt)}</td><td>{window.affectedBookings}</td>
          <td><Tag tone={status === "Active" ? "outline" : status === "Planned" ? "accent" : "neutral"}>{status}</Tag></td>
          {canEdit && <td style={{ whiteSpace: "nowrap" }}>{status === "Planned" && <>
            <button type="button" className="btn btn-ghost" onClick={() => startEdit(window)}>Edit</button>
            <button type="button" className="btn btn-ghost" onClick={() => setCancelling(window)}>Cancel</button>
          </>}</td>}</tr>; })}
      </tbody></table></div><Pagination showing={`Showing ${first}–${first + items.length - 1} of ${result!.totalItems}`}
        disablePrevious={result!.page <= 1} disableNext={result!.page >= result!.totalPages}
        onPrevious={() => setPage((value) => value - 1)} onNext={() => setPage((value) => value + 1)} /></>}
      {!canEdit && notice && <div className="notice-accent" role="status">{notice}</div>}
    </div>
    {canEdit && <Tile label={editing ? "Edit window" : "Schedule a window"} accented>
      <Field label="Room"><select className="input" aria-label="Room" value={roomId} disabled={editing !== null} onChange={(e) => setRoomId(e.target.value)}>{rooms.map((room) => <option key={room.id} value={room.id}>{room.name} — {room.building}, floor {room.floor}</option>)}</select></Field>
      <Field label="Reason"><input className="input" aria-label="Reason" value={reason} onChange={(e) => setReason(e.target.value)} /></Field>
      <div className="k2"><Field label="From"><input className="input" aria-label="From" type="datetime-local" value={startsAt} onChange={(e) => setStartsAt(e.target.value)} /></Field>
        <Field label="To"><input className="input" aria-label="To" type="datetime-local" value={endsAt} onChange={(e) => setEndsAt(e.target.value)} /></Field></div>
      {formError && <p role="alert" className="form-error">{formError}</p>}
      {notice && <div className="notice-accent" role="status">{notice}</div>}
      <button type="button" className="btn btn-primary btn-block" style={{ padding: 11 }} disabled={saving || rooms.length === 0} onClick={save}>{saving ? "Saving…" : editing ? "Save changes" : "Save window"}</button>
      {editing && <button type="button" className="btn btn-secondary btn-block" onClick={resetForm}>Stop editing</button>}
    </Tile>}</div>

    {overlap && <Dialog title="This window overlaps confirmed bookings" width={560} onClose={() => setOverlap(null)} actions={<>
      <button type="button" className="btn btn-secondary" onClick={() => setOverlap(null)}>Go back</button>
      <button type="button" className="btn btn-primary" disabled={saving} onClick={confirmOverlap}>{saving ? "Saving…" : "Schedule anyway and email the students"}</button>
    </>}>
      <p style={{ marginTop: 0 }}>These students hold the room during this time. They stay booked, and each gets an email about the maintenance.</p>
      <div className="table-scroll"><table className="table"><thead><tr><th>Student</th><th>From</th><th>To</th></tr></thead><tbody>
        {overlap.bookings.map((b) => <tr key={b.bookingId}><td>{b.studentName}</td><td>{colomboStamp(b.startsAt)}</td><td>{colomboStamp(b.endsAt)}</td></tr>)}
      </tbody></table></div>
    </Dialog>}

    {cancelling && <Dialog title="Cancel this maintenance window?" onClose={() => setCancelling(null)} actions={<>
      <button type="button" className="btn btn-secondary" onClick={() => setCancelling(null)}>Keep it</button>
      <button type="button" className="btn btn-primary" disabled={saving} onClick={confirmCancel}>Cancel window</button>
    </>}>
      <p style={{ margin: 0 }}>{cancelling.roomName}: {cancelling.reason}, {colomboStamp(cancelling.startsAt)}. The audit log keeps a record.</p>
    </Dialog>}
  </Screen>;
}

/** The bookings listed by a 409 maintenance-overlaps-bookings, or null for any other error. */
function overlappingBookings(error: unknown): OverlappingBooking[] | null {
  if (!(error instanceof ApiError) || error.status !== 409) return null;
  const problem = error.problem as { type?: string; bookings?: OverlappingBooking[] };
  return problem.type?.endsWith("maintenance-overlaps-bookings") && Array.isArray(problem.bookings) ? problem.bookings : null;
}

/** An ISO instant as the value a datetime-local input expects, in the browser's own time. */
function toLocalInput(iso: string) {
  const date = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function windowStatus(window: MaintenanceWindow) {
  const now = Date.now();
  if (new Date(window.endsAt).getTime() <= now) return "Finished";
  if (new Date(window.startsAt).getTime() <= now) return "Active";
  return "Planned";
}
function messageOf(error: unknown, fallback: string) { return error instanceof ApiError ? error.message : fallback; }
