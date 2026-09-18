import { useCallback, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { ApiError } from "../../api/client";
import {
  assignRoomEquipment, getRoom, getRoomSchedule, listEquipment, removeRoomEquipment, updateRoom,
  type EquipmentType, type RoomDetail, type ScheduleSlot,
} from "../../api/rooms";
import { Screen } from "../../components/AppShell";
import { Dialog, Field, KeyValue, Tag, Tile } from "../../components/ui";
import { useAuthStore } from "../../store/authStore";

/** W-14 · Live room detail, equipment assignment and upcoming schedule. Owned by S2. */
export function RoomDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const token = useAuthStore((s) => s.accessToken);
  const role = useAuthStore((s) => s.user?.role);
  const [room, setRoom] = useState<RoomDetail | null>(null);
  const [schedule, setSchedule] = useState<ScheduleSlot[]>([]);
  const [catalogue, setCatalogue] = useState<EquipmentType[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [editOpen, setEditOpen] = useState(false);
  const [assignOpen, setAssignOpen] = useState(false);
  const [equipmentId, setEquipmentId] = useState("");
  const [quantity, setQuantity] = useState(1);

  const load = useCallback(async () => {
    if (!token || !id) return;
    setLoading(true);
    setError(null);
    const from = new Date();
    const to = new Date(from.getTime() + 7 * 86400000);
    try {
      const [detail, slots, equipment] = await Promise.all([
        getRoom(token, id),
        getRoomSchedule(token, id, from.toISOString(), to.toISOString()),
        listEquipment(token, { pageSize: 100, sortBy: "name", sortDir: "asc" }),
      ]);
      setRoom(detail);
      setSchedule(slots);
      setCatalogue(equipment.items.filter((item) => item.isActive));
      setEquipmentId((current) => current || equipment.items.find((item) => item.isActive)?.id || "");
    } catch (err) {
      setError(messageOf(err, "Failed to load room details."));
    } finally {
      setLoading(false);
    }
  }, [id, token]);

  useEffect(() => { void load(); }, [load]);

  async function saveRoom(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token || !id || !room) return;
    const values = new FormData(event.currentTarget);
    try {
      await updateRoom(token, id, {
        name: String(values.get("name") ?? ""), building: String(values.get("building") ?? ""),
        floor: Number(values.get("floor")), capacity: Number(values.get("capacity")),
        hourlyRate: Number(values.get("hourlyRate")), qrCode: String(values.get("qrCode") ?? ""),
        isActive: values.get("isActive") === "on",
      });
      setEditOpen(false);
      await load();
    } catch (err) { setError(messageOf(err, "Failed to update the room.")); }
  }

  async function assign() {
    if (!token || !id || !equipmentId || quantity <= 0) return;
    try {
      await assignRoomEquipment(token, id, { equipmentTypeId: equipmentId, quantity });
      setAssignOpen(false);
      await load();
    } catch (err) { setError(messageOf(err, "Failed to assign equipment.")); }
  }

  async function remove(equipmentTypeId: string) {
    if (!token || !id) return;
    try { await removeRoomEquipment(token, id, equipmentTypeId); await load(); }
    catch (err) { setError(messageOf(err, "Failed to remove equipment.")); }
  }

  return <Screen title={room ? `Room ${room.name}` : `Room ${id ?? ""}`} crumb="Rooms" onBack={() => navigate("/rooms")} showUser={false}
    actions={room && <>
      <button type="button" className="btn btn-secondary" onClick={() => navigate("/maintenance")}>Schedule maintenance</button>
      <button type="button" className="btn btn-secondary" onClick={() => setEditOpen(true)}>Edit room</button>
      <button type="button" className="btn btn-primary" onClick={() => navigate(`/rooms/calendar?room=${room.id}`)}>View calendar</button>
    </>}>
    {error && <p role="alert" className="form-error">{error}</p>}
    {loading && !room && <div className="state-view">Loading…</div>}
    {room && <div className="split-wide" style={{ gridTemplateColumns: "1fr 1.3fr" }}>
      <Tile label="Room information">
        <KeyValue label="Building">{room.building}</KeyValue><KeyValue label="Floor">{room.floor}</KeyValue>
        <KeyValue label="Seats">{room.capacity}</KeyValue><KeyValue label="Rate">Rs. {room.hourlyRate.toFixed(2)} / hour</KeyValue>
        <KeyValue label="QR code">{room.qrCode}</KeyValue><KeyValue label="Status"><Tag tone={room.isActive ? "accent" : "neutral"}>{room.isActive ? "Active" : "Inactive"}</Tag></KeyValue>
      </Tile>
      <div className="stack">
        <Tile label="Installed equipment" action={role === "Librarian" && <button type="button" className="btn btn-secondary" onClick={() => setAssignOpen(true)}>Add equipment</button>}>
          {room.equipment.length === 0 ? <div className="state-view">No equipment assigned.</div> : <div className="table-scroll"><table className="table"><thead><tr><th>Equipment</th><th>Quantity</th><th /></tr></thead><tbody>
            {room.equipment.map((item) => <tr key={item.equipmentTypeId}><td>{item.name}</td><td>{item.quantity}</td><td>{role === "Librarian" && <button type="button" className="btn btn-ghost" onClick={() => remove(item.equipmentTypeId)}>Remove</button>}</td></tr>)}
          </tbody></table></div>}
        </Tile>
        <Tile label="Next seven days">
          {schedule.length === 0 ? <div className="state-view">No bookings or maintenance scheduled.</div> : <div className="table-scroll"><table className="table"><thead><tr><th>From</th><th>To</th><th>Type</th></tr></thead><tbody>
            {schedule.map((slot) => <tr key={`${slot.startsAt}-${slot.kind}`}><td>{formatDate(slot.startsAt)}</td><td>{formatDate(slot.endsAt)}</td><td><Tag tone={slot.kind === "Maintenance" ? "outline" : "accent"}>{slot.kind}</Tag></td></tr>)}
          </tbody></table></div>}
        </Tile>
      </div>
    </div>}
    {editOpen && room && <Dialog title="Edit room" width={520} onClose={() => setEditOpen(false)}><form onSubmit={saveRoom} className="stack">
      <div className="k2"><EditField name="name" label="Room name" value={room.name} /><EditField name="building" label="Building" value={room.building} />
        <EditField name="floor" label="Floor" value={String(room.floor)} type="number" /><EditField name="capacity" label="Seats" value={String(room.capacity)} type="number" />
        <EditField name="hourlyRate" label="Rate per hour (Rs.)" value={String(room.hourlyRate)} type="number" /><EditField name="qrCode" label="QR code" value={room.qrCode} /></div>
      <label className="radio"><input name="isActive" type="checkbox" defaultChecked={room.isActive} /> Active</label>
      <div className="dialog-actions"><button type="button" className="btn btn-secondary" onClick={() => setEditOpen(false)}>Cancel</button><button className="btn btn-primary" type="submit">Save changes</button></div>
    </form></Dialog>}
    {assignOpen && <Dialog title="Add equipment" onClose={() => setAssignOpen(false)} actions={<><button type="button" className="btn btn-secondary" onClick={() => setAssignOpen(false)}>Cancel</button><button type="button" className="btn btn-primary" onClick={assign}>Assign</button></>}>
      <Field label="Equipment"><select className="input" aria-label="Equipment" value={equipmentId} onChange={(e) => setEquipmentId(e.target.value)}>{catalogue.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></Field>
      <Field label="Quantity"><input className="input" aria-label="Quantity" type="number" min={1} value={quantity} onChange={(e) => setQuantity(Number(e.target.value))} /></Field>
    </Dialog>}
  </Screen>;
}

function EditField({ name, label, value, type = "text" }: { name: string; label: string; value: string; type?: string }) {
  return <Field label={label}><input className="input" name={name} aria-label={label} type={type} min={type === "number" ? 0 : undefined} defaultValue={value} required /></Field>;
}
function messageOf(error: unknown, fallback: string) { return error instanceof ApiError ? error.message : fallback; }
function formatDate(value: string) { return new Date(value).toLocaleString(); }
