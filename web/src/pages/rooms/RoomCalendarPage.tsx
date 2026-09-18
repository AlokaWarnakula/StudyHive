import { useEffect, useMemo, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { ApiError } from "../../api/client";
import { getRoomSchedule, listRooms, type Room, type ScheduleSlot } from "../../api/rooms";
import { Screen } from "../../components/AppShell";
import { Icon } from "../../components/Icon";
import { Tag } from "../../components/ui";
import { useAuthStore } from "../../store/authStore";

/** W-15 · Live weekly booking and maintenance calendar for a selected room. Owned by S2. */
export function RoomCalendarPage() {
  const navigate = useNavigate();
  const token = useAuthStore((s) => s.accessToken);
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

  return <Screen title="Room calendar" onBack={() => navigate("/rooms")} showUser={false} actions={<>
    <button type="button" className="btn btn-secondary" onClick={() => setWeekOffset(0)}>Today</button>
    <button type="button" className="btn btn-ghost btn-icon" aria-label="Previous week" onClick={() => setWeekOffset((value) => value - 1)}><Icon name="chevron-left" /></button>
    <b style={{ fontSize: 14 }}>{week.label}</b>
    <button type="button" className="btn btn-ghost btn-icon" aria-label="Next week" onClick={() => setWeekOffset((value) => value + 1)}><Icon name="chevron-right" /></button>
    <select className="input" style={{ width: 180 }} aria-label="Room filter" value={selectedRoom} onChange={(e) => setParams({ room: e.target.value })}>
      {rooms.map((room) => <option key={room.id} value={room.id}>{room.name}</option>)}
    </select>
  </>}>
    <div className="bar" style={{ fontSize: 12, gap: 16 }}><span><Tag tone="accent">Booked</Tag></span><span><Tag tone="outline">Maintenance</Tag></span></div>
    {error && <p role="alert" className="form-error">{error}</p>}
    {loading && <div className="state-view">Loading…</div>}
    {!loading && rooms.length === 0 && <div className="state-view">No active rooms are available.</div>}
    {!loading && rooms.length > 0 && slots.length === 0 && <div className="state-view">No bookings or maintenance this week.</div>}
    {slots.length > 0 && <div className="table-scroll"><table className="table"><thead><tr><th>Date</th><th>From</th><th>To</th><th>Room</th><th>Type</th></tr></thead><tbody>
      {slots.map((slot) => <tr key={`${slot.startsAt}-${slot.endsAt}-${slot.kind}`}><td>{new Date(slot.startsAt).toLocaleDateString()}</td>
        <td>{new Date(slot.startsAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}</td>
        <td>{new Date(slot.endsAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}</td><td>{slot.roomName}</td>
        <td><Tag tone={slot.kind === "Maintenance" ? "outline" : "accent"}>{slot.kind}</Tag></td></tr>)}
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
