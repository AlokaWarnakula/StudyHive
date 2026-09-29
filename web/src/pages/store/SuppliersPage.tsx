import { useEffect, useState } from "react";
import { Screen } from "../../components/AppShell";
import { Icon } from "../../components/Icon";
import { Dialog, Field, Pagination, Tag, Toolbar } from "../../components/ui";
import {
  createSupplier,
  listSuppliers,
  updateSupplier,
  type PagedResult,
  type Supplier,
  type SupplierWriteBody,
} from "../../api/consumables";
import { useAuthStore } from "../../store/authStore";
import { messageOf } from "./storeUtils";

const PAGE_SIZE = 20;

/**
 * W-23 · Suppliers — GET / POST / PUT /api/suppliers with search, an active/all filter, sort and
 * pagination. Add and edit share one dialog; deactivating is an edit (suppliers are never deleted).
 * Owned by S3.
 */
export function SuppliersPage() {
  const token = useAuthStore((s) => s.accessToken);

  const [result, setResult] = useState<PagedResult<Supplier> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState("");
  const [activeOnly, setActiveOnly] = useState(true);
  const [sortDir, setSortDir] = useState<"asc" | "desc">("asc");
  const [page, setPage] = useState(1);
  const [reload, setReload] = useState(0);

  const [dialog, setDialog] = useState<{ editing: Supplier | null } | null>(null);

  useEffect(() => {
    if (!token) return;
    let cancelled = false;
    setLoading(true);
    setError(null);

    listSuppliers(token, { page, pageSize: PAGE_SIZE, search: search || undefined, activeOnly, sortBy: "name", sortDir })
      .then((data) => {
        if (!cancelled) setResult(data);
      })
      .catch((err) => {
        if (!cancelled) setError(messageOf(err, "Failed to load suppliers."));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [token, page, search, activeOnly, sortDir, reload]);

  async function save(body: SupplierWriteBody, isActive: boolean) {
    if (!token || !dialog) return;
    if (dialog.editing) await updateSupplier(token, dialog.editing.id, { ...body, isActive });
    else await createSupplier(token, body);
    setDialog(null);
    setReload((n) => n + 1);
  }

  const items = result?.items ?? [];
  const firstRow = result && result.totalItems > 0 ? (result.page - 1) * result.pageSize + 1 : 0;

  return (
    <Screen
      title="Suppliers"
      crumb={result ? `${result.totalItems} ${activeOnly ? "active" : "in total"}` : undefined}
      showUser={false}
      actions={
        <button type="button" className="btn btn-primary" onClick={() => setDialog({ editing: null })}>
          <Icon name="plus" size={16} />
          Add supplier
        </button>
      }
    >
      <Toolbar>
        <input
          className="input"
          style={{ maxWidth: 250 }}
          type="search"
          placeholder="Search supplier"
          aria-label="Search supplier"
          value={search}
          onChange={(e) => {
            setPage(1);
            setSearch(e.target.value);
          }}
        />
        <select
          className="input"
          style={{ maxWidth: 170 }}
          aria-label="Supplier status"
          value={activeOnly ? "active" : "all"}
          onChange={(e) => {
            setPage(1);
            setActiveOnly(e.target.value === "active");
          }}
        >
          <option value="active">Status: Active</option>
          <option value="all">Status: All</option>
        </select>
      </Toolbar>

      {error && (
        <p role="alert" className="form-error">
          {error}
        </p>
      )}

      {loading && !result && <div className="state-view">Loading…</div>}

      {result && items.length === 0 && !loading && <div className="state-view">No supplier matches these filters.</div>}

      {items.length > 0 && (
        <>
          <div className="table-scroll">
            <table className="table">
              <thead>
                <tr>
                  <th>
                    <button type="button" onClick={() => setSortDir((d) => (d === "asc" ? "desc" : "asc"))}>
                      Supplier {sortDir === "asc" ? "▲" : "▼"}
                    </button>
                  </th>
                  <th>Email</th>
                  <th>Phone</th>
                  <th>Address</th>
                  <th>Status</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {items.map((s) => (
                  <tr key={s.id}>
                    <td>
                      <b>{s.name}</b>
                    </td>
                    <td>{s.contactEmail}</td>
                    <td>{s.phone}</td>
                    <td>{s.address || "—"}</td>
                    <td>
                      <Tag tone={s.isActive ? "accent" : "neutral"}>{s.isActive ? "Active" : "Inactive"}</Tag>
                    </td>
                    <td>
                      <button type="button" className="btn btn-ghost" onClick={() => setDialog({ editing: s })}>
                        Edit
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <Pagination
            showing={`Showing ${firstRow}–${firstRow + items.length - 1} of ${result!.totalItems}`}
            disablePrevious={result!.page <= 1}
            disableNext={result!.page >= result!.totalPages}
            onPrevious={() => setPage((p) => p - 1)}
            onNext={() => setPage((p) => p + 1)}
          />
        </>
      )}

      {dialog && <SupplierDialog initial={dialog.editing} onClose={() => setDialog(null)} onSave={save} />}
    </Screen>
  );
}

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function SupplierDialog({
  initial,
  onClose,
  onSave,
}: {
  initial: Supplier | null;
  onClose: () => void;
  onSave: (body: SupplierWriteBody, isActive: boolean) => Promise<void>;
}) {
  const [name, setName] = useState(initial?.name ?? "");
  const [contactEmail, setContactEmail] = useState(initial?.contactEmail ?? "");
  const [phone, setPhone] = useState(initial?.phone ?? "");
  const [address, setAddress] = useState(initial?.address ?? "");
  const [isActive, setIsActive] = useState(initial?.isActive ?? true);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function save() {
    // Mirrors CreateSupplierRequest / UpdateSupplierRequest.
    if (!name.trim()) return setError("Name is required.");
    if (name.trim().length > 120) return setError("Name must be 120 characters or fewer.");
    if (!EMAIL.test(contactEmail.trim())) return setError("Enter a valid contact email.");
    if (!phone.trim()) return setError("Phone is required.");
    if (phone.trim().length > 30) return setError("Phone must be 30 characters or fewer.");

    setError(null);
    setSaving(true);
    try {
      await onSave(
        { name: name.trim(), contactEmail: contactEmail.trim(), phone: phone.trim(), address: address.trim() || null },
        isActive,
      );
    } catch (err) {
      setError(messageOf(err, "Failed to save the supplier."));
      setSaving(false);
    }
  }

  return (
    <Dialog
      title={initial ? `Edit ${initial.name}` : "Add supplier"}
      width={500}
      onClose={onClose}
      actions={
        <>
          <button type="button" className="btn btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button type="button" className="btn btn-primary" onClick={save} disabled={saving}>
            {saving ? "Saving…" : "Save supplier"}
          </button>
        </>
      }
    >
      <Field label="Name">
        <input className="input" aria-label="Name" value={name} onChange={(e) => setName(e.target.value)} />
      </Field>
      <div className="k2">
        <Field label="Contact email">
          <input
            className="input"
            type="email"
            aria-label="Contact email"
            value={contactEmail}
            onChange={(e) => setContactEmail(e.target.value)}
          />
        </Field>
        <Field label="Phone">
          <input className="input" aria-label="Phone" value={phone} onChange={(e) => setPhone(e.target.value)} />
        </Field>
      </div>
      <Field label="Address">
        <textarea className="input" aria-label="Address" value={address} onChange={(e) => setAddress(e.target.value)} />
      </Field>
      {initial && (
        <label className="radio">
          <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} /> Active
        </label>
      )}
      {error && (
        <p role="alert" className="form-error">
          {error}
        </p>
      )}
    </Dialog>
  );
}
