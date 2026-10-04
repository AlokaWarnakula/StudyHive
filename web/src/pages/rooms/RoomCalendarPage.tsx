import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { ApiError } from "../../api/client";
import { getRoomSchedule, listRooms, type Room, type ScheduleSlot } from "../../api/rooms";
import { Screen } from "../../components/AppShell";
import { Icon } from "../../components/Icon";
import { Tag } from "../../components/ui";
import { can } from "../../auth/permissions";
import { useAuthStore } from "../../store/authStore";
import { colomboDay, colomboTime } from "../../utils/colomboTime";

/**
 * W-15 · Live weekly booking and maintenance calendar for a selected room. Owned by S2.
 *
 * AUDIT CW-12: each booking says who booked it, for what, and whether they checked in or were a
 * no-show (staff-only detail from the schedule API); times are Colombo time.
 */
export function RoomCalendarPage() {
  const navigate = useNavigate();
  const token = useAuthStore((s) => s.accessToken);
  const role = useAuthStore((s) => s.user?.role);
  const [params, setParams] = useSearchParams();
  const [rooms, setRooms] = useState<Room[]>([]);
  const [slots, setSlots] = useState<ScheduleSlot[]>([]);
  const [weekOffset, setWeekOffset] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const selectedRoom = params.get("room") ?? "";
  const week = useMemo(() => weekRange(weekOffset), [weekOffset]);

  useEffect(() => {
    if (!token) return;
    let cancelled = false;
    listRooms(token, { pageSize: 100, sortBy: "name", sortDir: "asc" })
      .then((result) => {
        if (cancelled) return;
        const active = result.items.filter((room) => room.isActive);
        setRooms(active);
        if (!selectedRoom && active[0]) setParams({ room: active[0].id }, { replace: true });
      })
      .catch((err) => !cancelled && setError(messageOf(err, "Failed to load rooms.")));
    return () => { cancelled = true; };
  }, [token, selectedRoom, setParams]);

  useEffect(() => {
    if (!token || !selectedRoom) { setLoading(false); return; }
    let cancelled = false;
    setLoading(true);
    setError(null);
    getRoomSchedule(token, selectedRoom, week.start.toISOString(), week.end.toISOString())
      .then((data) => !cancelled && setSlots(data))
      .catch((err) => !cancelled && setError(messageOf(err, "Failed to load the room schedule.")))
      .finally(() => !cancelled && setLoading(false));
    return () => { cancelled = true; };
  }, [token, selectedRoom, week]);

  return <Screen title="Room calendar" onBack={() => navigate("/rooms")} actions={<>
    <button type="button" className="btn btn-secondary" onClick={() => setWeekOffset(0)}>Today</button>
    <button type="button" className="btn btn-ghost btn-icon" aria-label="Previous week" onClick={() => setWeekOffset((value) => value - 1)}><Icon name="chevron-left" /></button>
    <b style={{ fontSize: 14 }}>{week.label}</b>
    <button type="button" className="btn btn-ghost btn-icon" aria-label="Next week" onClick={() => setWeekOffset((value) => value + 1)}><Icon name="chevron-right" /></button>
    <select className="input" style={{ width: 180 }} aria-label="Room filter" value={selectedRoom} onChange={(e) => setParams({ room: e.target.value })}>
      {rooms.map((room) => <option key={room.id} value={room.id}>{room.name}</option>)}
    </select>
  </>}>
    <div className="bar" style={{ fontSize: 12, gap: 16 }}><span><Tag tone="accent">Booked</Tag></span><span><Tag tone="accent">Checked in</Tag></span><span><Tag tone="outline">No-show</Tag></span><span><Tag tone="outline">Maintenance</Tag></span></div>
    {error && <p role="alert" className="form-error">{error}</p>}
    {loading && <div className="state-view">Loading…</div>}
    {!loading && rooms.length === 0 && <div className="state-view">No active rooms are available.</div>}
    {!loading && rooms.length > 0 && slots.length === 0 && <div className="state-view">No bookings or maintenance this week.</div>}
    {slots.length > 0 && <div className="table-scroll"><table className="table"><thead><tr><th>Date</th><th>From</th><th>To</th><th>Booked by</th><th>Purpose</th><th>Status</th></tr></thead><tbody>
      {slots.map((slot) => <tr key={`${slot.startsAt}-${slot.endsAt}-${slot.kind}-${slot.bookingRequestId ?? ""}`}><td>{colomboDay(slot.startsAt)}</td>
        <td>{colomboTime(slot.startsAt)}</td><td>{colomboTime(slot.endsAt)}</td>
        <td>{slot.studentName ?? (slot.kind === "Maintenance" ? "Library" : "—")}</td>
        <td>{slot.bookingRequestId && can(role, "requests.view")
          ? <Link to={`/requests/${slot.bookingRequestId}`}>{slot.objective ?? "Open request"}</Link>
          : slot.objective ?? (slot.kind === "Maintenance" ? "Maintenance" : "—")}</td>
        <td><Tag tone={slotTone(slot)}>{slotLabel(slot)}</Tag></td></tr>)}
    </tbody></table></div>}
  </Screen>;
}

function weekRange(offset: number) {
  const today = new Date();
  const start = new Date(today.getFullYear(), today.getMonth(), today.getDate());
  const day = (start.getDay() + 6) % 7;
  start.setDate(start.getDate() - day + offset * 7);
  const end = new Date(start); end.setDate(end.getDate() + 7);
  return { start, end, label: `${start.toLocaleDateString()} – ${new Date(end.getTime() - 1).toLocaleDateString()}` };
}
function messageOf(error: unknown, fallback: string) { return error instanceof ApiError ? error.message : fallback; }

function slotLabel(slot: ScheduleSlot): string {
  if (slot.kind === "Maintenance") return "Maintenance";
  if (slot.checkedInAt) return `Checked in ${colomboTime(slot.checkedInAt)}`;
  if (slot.bookingStatus === "NoShow") return "No-show";
  if (slot.bookingStatus === "Completed") return "Completed";
  return "Booked";
}

function slotTone(slot: ScheduleSlot): "accent" | "outline" | "neutral" {
  if (slot.kind === "Maintenance" || slot.bookingStatus === "NoShow") return "outline";
  if (slot.bookingStatus === "Completed" && !slot.checkedInAt) return "neutral";
  return "accent";
}
