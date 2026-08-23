import { useEffect, useState } from "react";
import { Screen } from "../../components/AppShell";
import { Field, KeyValue, Pagination, Placeholder, Tag, Toolbar } from "../../components/ui";
import { Icon } from "../../components/Icon";
import { ApiError } from "../../api/client";
import {
  listStudentProfiles,
  updateStudentProfile,
  type PagedResult,
  type StudentProfile,
} from "../../api/studentProfiles";
import { useAuthStore } from "../../store/authStore";

const PAGE_SIZE = 20;

/**
 * W-12 · Students — GET /api/student-profiles. A row opens the side panel; the weekly limit and
 * account status are editable by an Admin only, matching PUT /api/student-profiles/{id}.
 *
 * A real S1 screen: list, panel and save all talk to the API.
 */
export function StudentsPage() {
  const token = useAuthStore((s) => s.accessToken);
  const isAdmin = useAuthStore((s) => s.user?.role) === "Admin";

  const [result, setResult] = useState<PagedResult<StudentProfile> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);

  const [selected, setSelected] = useState<StudentProfile | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveMessage, setSaveMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!token) return;
    let cancelled = false;
    setLoading(true);
    setError(null);

    listStudentProfiles(token, { page, pageSize: PAGE_SIZE, search: search || undefined })
      .then((data) => {
        if (!cancelled) setResult(data);
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof ApiError ? err.message : "Failed to load student profiles.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [token, page, search]);

  async function handleSave(limit: number, active: boolean) {
    if (!token || !selected) return;
    setSaving(true);
    setSaveMessage(null);
    try {
      const updated = await updateStudentProfile(token, selected.id, {
        department: selected.department,
        yearOfStudy: selected.yearOfStudy,
        maxBookingsPerWeek: limit,
        penaltyPoints: selected.penaltyPoints,
        suspendedUntil: selected.suspendedUntil,
        isActive: active,
      });
      setSelected(updated);
      setResult((r) => (r ? { ...r, items: r.items.map((i) => (i.id === updated.id ? updated : i)) } : r));
      setSaveMessage("Saved.");
    } catch (err) {
      setSaveMessage(err instanceof ApiError ? err.message : "Could not save this profile.");
    } finally {
      setSaving(false);
    }
  }

  const items = result?.items ?? [];
  const firstRow = result && result.totalItems > 0 ? (result.page - 1) * result.pageSize + 1 : 0;

  return (
    <Screen title="Students" crumb={result ? `${result.totalItems} registered` : undefined}>
      <div className="split" style={{ gridTemplateColumns: selected ? "1fr 340px" : "1fr" }}>
        <div className="stack">
          <Toolbar>
            <input
              className="input"
              style={{ maxWidth: 250 }}
              type="search"
              placeholder="Search student number or department"
              aria-label="Search student number or department"
              value={search}
              onChange={(e) => {
                setPage(1);
                setSearch(e.target.value);
              }}
            />
          </Toolbar>

          {error && (
            <p role="alert" className="form-error">
              {error}
            </p>
          )}
          {loading && !result && <div className="state-view">Loading…</div>}
          {result && items.length === 0 && !loading && (
            <div className="state-view">No student profile matches this search.</div>
          )}

          {items.length > 0 && (
            <>
              <div className="table-scroll">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Student number</th>
                      <th>Department</th>
                      <th>Year</th>
                      <th>Limit</th>
                      <th>Penalties</th>
                      <th>Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {items.map((p) => (
                      <tr
                        key={p.id}
                        className="row-click"
                        onClick={() => {
                          setSaveMessage(null);
                          setSelected(p);
                        }}
                        style={selected?.id === p.id ? { background: "var(--color-accent-100)" } : undefined}
                      >
                        <td>
                          <b>{p.studentNumber}</b>
                        </td>
                        <td>{p.department}</td>
                        <td>{p.yearOfStudy}</td>
                        <td>{p.maxBookingsPerWeek}</td>
                        <td>{p.penaltyPoints}</td>
                        <td>
                          {p.isActive ? <Tag tone="accent">Active</Tag> : <Tag tone="neutral">Suspended</Tag>}
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
        </div>

        {selected && (
          <StudentPanel
            key={selected.id}
            profile={selected}
            isAdmin={isAdmin}
            saving={saving}
            message={saveMessage}
            onClose={() => setSelected(null)}
            onSave={handleSave}
          />
        )}
      </div>
    </Screen>
  );
}

function StudentPanel({
  profile,
  isAdmin,
  saving,
  message,
  onClose,
  onSave,
}: {
  profile: StudentProfile;
  isAdmin: boolean;
  saving: boolean;
  message: string | null;
  onClose: () => void;
  onSave: (limit: number, active: boolean) => void;
}) {
  const [limit, setLimit] = useState(String(profile.maxBookingsPerWeek));
  const [active, setActive] = useState(profile.isActive);

  return (
    <div className="tile" style={{ borderColor: "var(--color-accent)" }}>
      <div className="bar">
        <span className="lbl">Student profile</span>
        <button type="button" className="btn btn-ghost btn-icon" style={{ marginLeft: "auto" }} onClick={onClose} aria-label="Close panel">
          <Icon name="x" />
        </button>
      </div>

      <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
        <Placeholder width={52} height={52} />
        <div>
          <b style={{ fontSize: 17 }}>{profile.studentNumber}</b>
          <div className="fnote">{profile.department}</div>
        </div>
      </div>

      <hr className="hr" />
      <KeyValue label="Department">{profile.department}</KeyValue>
      <KeyValue label="Year">{String(profile.yearOfStudy)}</KeyValue>
      <KeyValue label="Joined">{new Date(profile.createdAt).toLocaleDateString()}</KeyValue>
      <KeyValue label="Penalties">{profile.penaltyPoints === 0 ? "None" : String(profile.penaltyPoints)}</KeyValue>
      <KeyValue label="Suspended until">{profile.suspendedUntil ?? "—"}</KeyValue>
      <hr className="hr" />

      <Field label="Bookings allowed per week (admin only)">
        <input
          className="input"
          value={limit}
          disabled={!isAdmin}
          aria-label="Bookings allowed per week"
          onChange={(e) => setLimit(e.target.value)}
        />
      </Field>
      <Field label="Account status">
        <select
          className="input"
          value={active ? "Active" : "Suspended"}
          disabled={!isAdmin}
          aria-label="Account status"
          onChange={(e) => setActive(e.target.value === "Active")}
        >
          <option>Active</option>
          <option>Suspended</option>
        </select>
      </Field>

      {message && (
        <p className="fnote" role="status">
          {message}
        </p>
      )}

      <button
        type="button"
        className="btn btn-primary btn-block"
        disabled={!isAdmin || saving}
        onClick={() => onSave(Number(limit), active)}
      >
        {saving ? "Saving…" : "Save changes"}
      </button>
      {!isAdmin && (
        <span className="fnote">
          Only an Admin can change a student's weekly limit or account status (PUT /api/student-profiles/&#123;id&#125;).
        </span>
      )}
    </div>
  );
}
