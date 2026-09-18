import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { ApiError } from "../../api/client";
import { createRoom, listEquipment, listRooms, type EquipmentType, type PagedResult, type Room, type RoomInput } from "../../api/rooms";
import { Screen } from "../../components/AppShell";
import { Icon } from "../../components/Icon";
import { Dialog, Field, Pagination, Tag, Toolbar } from "../../components/ui";
import { useAuthStore } from "../../store/authStore";

const PAGE_SIZE = 20;
const emptyForm: RoomInput = { name: "", building: "", floor: 1, capacity: 1, hourlyRate: 0, qrCode: "" };

/** W-13 · Live room list and add-room dialog. Owned by S2. */
export function RoomsPage() {
  const token = useAuthStore((s) => s.accessToken);
  const navigate = useNavigate();
  const [result, setResult] = useState<PagedResult<Room> | null>(null);
  const [search, setSearch] = useState("");
  const [minimumCapacity, setMinimumCapacity] = useState("");
  const [equipmentTypeId, setEquipmentTypeId] = useState("");
  const [equipmentTypes, setEquipmentTypes] = useState<EquipmentType[]>([]);
  const [page, setPage] = useState(1);
  const [sortBy, setSortBy] = useState("name");
  const [sortDir, setSortDir] = useState<"asc" | "desc">("asc");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [form, setForm] = useState<RoomInput>(emptyForm);
  const [saving, setSaving] = useState(false);
  const [reload, setReload] = useState(0);

  useEffect(() => {
    if (!token) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    listRooms(token, {
      page,
      pageSize: PAGE_SIZE,
      search: search || undefined,
      capacity: minimumCapacity ? Number(minimumCapacity) : undefined,
      equipmentTypeId: equipmentTypeId || undefined,
      sortBy,
      sortDir,
    })
      .then((data) => !cancelled && setResult(data))
      .catch((err) => !cancelled && setError(messageOf(err, "Failed to load rooms.")))
      .finally(() => !cancelled && setLoading(false));
    return () => { cancelled = true; };
  }, [token, page, search, minimumCapacity, equipmentTypeId, sortBy, sortDir, reload]);

  useEffect(() => {
    if (!token) return;
    listEquipment(token, { pageSize: 100, sortBy: "name", sortDir: "asc" })
      .then((data) => setEquipmentTypes(data.items.filter((item) => item.isActive)))
      .catch((err) => setError(messageOf(err, "Failed to load equipment filters.")));
  }, [token]);

  function toggleSort(column: string) {
    if (sortBy === column) setSortDir((value) => value === "asc" ? "desc" : "asc");
    else { setSortBy(column); setSortDir("asc"); }
    setPage(1);
  }

  async function save() {
    if (!token) return;
    if (!form.name.trim() || !form.building.trim() || !form.qrCode.trim() || form.capacity <= 0 || form.hourlyRate < 0) {
      setError("Enter a room name, building and QR code; capacity must be above 0 and rate cannot be negative.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await createRoom(token, form);
      setDialogOpen(false);
      setForm(emptyForm);
      setPage(1);
      setReload((value) => value + 1);
    } catch (err) {
      setError(messageOf(err, "Failed to create the room."));
    } finally {
      setSaving(false);
    }
  }

  const items = result?.items ?? [];
  const first = result && result.totalItems > 0 ? (result.page - 1) * result.pageSize + 1 : 0;
  const sortMark = (column: string) => sortBy === column ? (sortDir === "asc" ? "▲" : "▼") : "";

  return (
    <Screen title="Rooms" crumb={result ? `${result.totalItems} rooms` : undefined} showUser={false}
      actions={<button type="button" className="btn btn-primary" onClick={() => setDialogOpen(true)}><Icon name="plus" size={16} />Add room</button>}>
      <Toolbar>
        <input className="input" style={{ maxWidth: 280 }} type="search" placeholder="Search name or building"
          aria-label="Search name or building" value={search} onChange={(e) => { setPage(1); setSearch(e.target.value); }} />
        <input className="input" style={{ maxWidth: 170 }} type="number" min="1" placeholder="Minimum seats"
          aria-label="Minimum capacity" value={minimumCapacity}
          onChange={(e) => { setPage(1); setMinimumCapacity(e.target.value); }} />
        <select className="input" style={{ maxWidth: 220 }} aria-label="Equipment type" value={equipmentTypeId}
          onChange={(e) => { setPage(1); setEquipmentTypeId(e.target.value); }}>
          <option value="">All equipment</option>
          {equipmentTypes.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
        </select>
      </Toolbar>
      {error && <p role="alert" className="form-error">{error}</p>}
      {loading && !result && <div className="state-view">Loading…</div>}
      {result && items.length === 0 && !loading && <div className="state-view">No rooms match this search.</div>}
      {items.length > 0 && <>
        <div className="table-scroll"><table className="table"><thead><tr>
          <th><button type="button" onClick={() => toggleSort("name")}>Room {sortMark("name")}</button></th>
          <th><button type="button" onClick={() => toggleSort("building")}>Building {sortMark("building")}</button></th>
          <th><button type="button" onClick={() => toggleSort("floor")}>Floor {sortMark("floor")}</button></th>
          <th><button type="button" onClick={() => toggleSort("capacity")}>Seats {sortMark("capacity")}</button></th>
          <th><button type="button" onClick={() => toggleSort("hourlyRate")}>Rate / hour {sortMark("hourlyRate")}</button></th>
          <th>Status</th><th />
        </tr></thead><tbody>{items.map((room) => <tr key={room.id} className="row-click" onClick={() => navigate(`/rooms/${room.id}`)}>
          <td><b>{room.name}</b></td><td>{room.building}</td><td>{room.floor}</td><td>{room.capacity}</td>
          <td>Rs. {room.hourlyRate.toFixed(2)}</td><td><Tag tone={room.isActive ? "accent" : "neutral"}>{room.isActive ? "Active" : "Inactive"}</Tag></td>
          <td><Link to={`/rooms/${room.id}`} onClick={(e) => e.stopPropagation()}>Open</Link></td>
        </tr>)}</tbody></table></div>
        <Pagination showing={`Showing ${first}–${first + items.length - 1} of ${result!.totalItems}`}
          disablePrevious={result!.page <= 1} disableNext={result!.page >= result!.totalPages}
          onPrevious={() => setPage((value) => value - 1)} onNext={() => setPage((value) => value + 1)} />
      </>}
      {dialogOpen && <Dialog title="Add room" width={520} onClose={() => setDialogOpen(false)} actions={<>
        <button type="button" className="btn btn-secondary" onClick={() => setDialogOpen(false)}>Cancel</button>
        <button type="button" className="btn btn-primary" disabled={saving} onClick={save}>{saving ? "Saving…" : "Save room"}</button>
      </>}><div className="k2">
        <RoomField label="Room name" value={form.name} onChange={(name) => setForm({ ...form, name })} />
        <RoomField label="Building" value={form.building} onChange={(building) => setForm({ ...form, building })} />
        <RoomField label="Floor" type="number" value={String(form.floor)} onChange={(value) => setForm({ ...form, floor: Number(value) })} />
        <RoomField label="Seats" type="number" value={String(form.capacity)} onChange={(value) => setForm({ ...form, capacity: Number(value) })} />
        <RoomField label="Rate per hour (Rs.)" type="number" value={String(form.hourlyRate)} onChange={(value) => setForm({ ...form, hourlyRate: Number(value) })} />
        <RoomField label="QR code" value={form.qrCode} onChange={(qrCode) => setForm({ ...form, qrCode })} />
      </div></Dialog>}
    </Screen>
  );
}

function RoomField({ label, value, onChange, type = "text" }: { label: string; value: string; onChange: (value: string) => void; type?: string }) {
  return <Field label={label}><input className="input" aria-label={label} type={type} value={value} onChange={(e) => onChange(e.target.value)} /></Field>;
}

function messageOf(error: unknown, fallback: string) { return error instanceof ApiError ? error.message : fallback; }
