import { useEffect, useState } from "react";
import { ApiError } from "../../api/client";
import {
  createEquipmentType, listEquipment, updateEquipmentType,
  type EquipmentType, type EquipmentTypeInput, type PagedResult,
} from "../../api/rooms";
import { Screen } from "../../components/AppShell";
import { Icon } from "../../components/Icon";
import { Dialog, Field, MetricTiles, Pagination, Tag, Toolbar } from "../../components/ui";
import { useAuthStore } from "../../store/authStore";

const PAGE_SIZE = 20;
const emptyForm: EquipmentTypeInput = { name: "", category: "", description: null };

/** W-16 · Live equipment-type catalogue with add/edit actions. Owned by S2. */
export function EquipmentPage() {
  const token = useAuthStore((s) => s.accessToken);
  const role = useAuthStore((s) => s.user?.role);
  const [result, setResult] = useState<PagedResult<EquipmentType> | null>(null);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<EquipmentType | null>(null);
  const [form, setForm] = useState<EquipmentTypeInput>(emptyForm);
  const [active, setActive] = useState(true);
  const [reload, setReload] = useState(0);

  useEffect(() => {
    if (!token) return;
    let cancelled = false;
    setLoading(true); setError(null);
    listEquipment(token, { page, pageSize: PAGE_SIZE, search: search || undefined, sortBy: "name", sortDir: "asc" })
      .then((data) => !cancelled && setResult(data))
      .catch((err) => !cancelled && setError(messageOf(err, "Failed to load equipment.")))
      .finally(() => !cancelled && setLoading(false));
    return () => { cancelled = true; };
  }, [token, page, search, reload]);

  function openCreate() { setEditing(null); setForm(emptyForm); setActive(true); setDialogOpen(true); }
  function openEdit(item: EquipmentType) {
    setEditing(item); setForm({ name: item.name, category: item.category, description: item.description });
    setActive(item.isActive); setDialogOpen(true);
  }
  async function save() {
    if (!token || !form.name.trim() || !form.category.trim()) { setError("Equipment name and category are required."); return; }
    try {
      if (editing) await updateEquipmentType(token, editing.id, { ...form, isActive: active });
      else await createEquipmentType(token, form);
      setDialogOpen(false); setReload((value) => value + 1);
    } catch (err) { setError(messageOf(err, "Failed to save equipment.")); }
  }

  const items = result?.items ?? [];
  const first = result && result.totalItems > 0 ? (result.page - 1) * result.pageSize + 1 : 0;
  const activeOnPage = items.filter((item) => item.isActive).length;
  return <Screen title="Equipment" crumb={result ? `${result.totalItems} equipment types` : undefined} showUser={false}
    actions={<button type="button" className="btn btn-primary" onClick={openCreate}><Icon name="plus" size={16} />Add equipment</button>}>
    {result && <MetricTiles metrics={[{ label: "Total types", value: String(result.totalItems) }, { label: "Active on page", value: String(activeOnPage) }, { label: "Inactive on page", value: String(items.length - activeOnPage) }]} columns={3} />}
    <Toolbar><input className="input" style={{ maxWidth: 280 }} type="search" placeholder="Search name, category or description" aria-label="Search equipment"
      value={search} onChange={(e) => { setPage(1); setSearch(e.target.value); }} /></Toolbar>
    {error && <p role="alert" className="form-error">{error}</p>}
    {loading && !result && <div className="state-view">Loading…</div>}
    {result && items.length === 0 && !loading && <div className="state-view">No equipment matches this search.</div>}
    {items.length > 0 && <><div className="table-scroll"><table className="table"><thead><tr><th>Name</th><th>Category</th><th>Description</th><th>Status</th><th>Updated</th><th /></tr></thead><tbody>
      {items.map((item) => <tr key={item.id}><td><b>{item.name}</b></td><td>{item.category}</td><td>{item.description || "—"}</td>
        <td><Tag tone={item.isActive ? "accent" : "neutral"}>{item.isActive ? "Active" : "Inactive"}</Tag></td><td>{new Date(item.updatedAt).toLocaleDateString()}</td>
        <td>{role === "Librarian" && <button type="button" className="btn btn-ghost" onClick={() => openEdit(item)}>Edit</button>}</td></tr>)}
    </tbody></table></div><Pagination showing={`Showing ${first}–${first + items.length - 1} of ${result!.totalItems}`}
      disablePrevious={result!.page <= 1} disableNext={result!.page >= result!.totalPages}
      onPrevious={() => setPage((value) => value - 1)} onNext={() => setPage((value) => value + 1)} /></>}
    {dialogOpen && <Dialog title={editing ? "Edit equipment" : "Add equipment"} width={500} onClose={() => setDialogOpen(false)} actions={<>
      <button type="button" className="btn btn-secondary" onClick={() => setDialogOpen(false)}>Cancel</button><button type="button" className="btn btn-primary" onClick={save}>Save equipment</button>
    </>}><Field label="Name"><input className="input" aria-label="Name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></Field>
      <Field label="Category"><input className="input" aria-label="Category" value={form.category} onChange={(e) => setForm({ ...form, category: e.target.value })} /></Field>
      <Field label="Description"><textarea className="input" aria-label="Description" value={form.description ?? ""} onChange={(e) => setForm({ ...form, description: e.target.value || null })} /></Field>
      {editing && <label className="radio"><input type="checkbox" checked={active} onChange={(e) => setActive(e.target.checked)} /> Active</label>}
    </Dialog>}
  </Screen>;
}

function messageOf(error: unknown, fallback: string) { return error instanceof ApiError ? error.message : fallback; }
