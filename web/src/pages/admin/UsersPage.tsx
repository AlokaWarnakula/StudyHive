import { useState } from "react";
import { Screen } from "../../components/AppShell";
import { Icon } from "../../components/Icon";
import {
  Dialog,
  Field,
  FixtureNotice,
  MetricTiles,
  NotBuiltYet,
  Pagination,
  Select,
  TagOf,
  Toolbar,
} from "../../components/ui";
import { useFixture } from "../../dev/useFixture";

/**
 * W-25 · Users & roles — GET /api/users, admin only. A role change writes an audit row, so the
 * dialog requires a reason before it will save.
 */
export function UsersPage() {
  const fixture = useFixture((f) => f.users);
  const [search, setSearch] = useState("");
  const [editing, setEditing] = useState<string | null>(null);
  const [reason, setReason] = useState("");
  const [formError, setFormError] = useState<string | null>(null);

  if (!fixture.enabled) {
    return (
      <Screen title="Users">
        <NotBuiltYet owner="Admin user-management" what="The user list" />
      </Screen>
    );
  }

  const u = fixture.data;
  const term = search.trim().toLowerCase();
  const rows = term ? u.rows.filter((r) => `${r.name} ${r.email}`.toLowerCase().includes(term)) : u.rows;

  function save() {
    // The reference marks this field "Required" — a role change with no recorded reason would
    // leave an audit row that explains nothing.
    if (!reason.trim()) {
      setFormError("A reason is required — it is written to the audit log.");
      return;
    }
    setFormError(null);
    setEditing(null);
    setReason("");
  }

  return (
    <Screen
      title="Users"
      crumb={`${u.total} accounts`}
      showUser={false}
      actions={
        <button type="button" className="btn btn-primary">
          <Icon name="plus" size={16} />
          Add user
        </button>
      }
    >
      <FixtureNotice owner="Admin" what="Accounts and roles" />

      <MetricTiles metrics={u.metrics} />

      <Toolbar>
        <input
          className="input"
          style={{ maxWidth: 250 }}
          placeholder="Search name or email"
          aria-label="Search name or email"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <Select label="Role" options={["All", "Student", "Librarian", "Store officer", "Admin"]} width={180} />
        <Select label="Status" options={["All", "Active", "Suspended"]} width={160} />
        <Select label="Sort" options={["Newest", "Name", "Last sign in"]} />
      </Toolbar>

      <div className="table-scroll">
        <table className="table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Email</th>
              <th>Role</th>
              <th>Last sign in</th>
              <th>Created</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.email}>
                <td>
                  <b>{r.name}</b>
                </td>
                <td>{r.email}</td>
                <td>
                  <TagOf tag={r.role} />
                </td>
                <td>{r.lastSignIn}</td>
                <td>{r.created}</td>
                <td>
                  <TagOf tag={r.status} />
                </td>
                <td>
                  <button type="button" className="btn btn-ghost" onClick={() => setEditing(r.name)}>
                    Edit
                  </button>
                </td>
              </tr>
            ))}
            {rows.length === 0 && (
              <tr>
                <td colSpan={7}>
                  <div className="state-view">No account matches “{search}”.</div>
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <Pagination showing={`Showing 1–${rows.length} of ${u.total}`} disablePrevious />

      {editing && (
        <Dialog
          title={`Change role — ${editing}`}
          body="Changing a role changes what this person can see immediately after their next sign in."
          width={440}
          onClose={() => setEditing(null)}
          actions={
            <>
              <button type="button" className="btn btn-secondary" onClick={() => setEditing(null)}>
                Cancel
              </button>
              <button type="button" className="btn btn-primary" onClick={save}>
                Save role
              </button>
            </>
          }
        >
          <Field label="Role">
            <div style={{ display: "flex", flexDirection: "column", gap: 8, marginTop: 4 }}>
              {u.roleChoices.map((c, i) => (
                <label className="radio" key={c.value}>
                  <input type="radio" name="role" defaultChecked={i === 0} />
                  <span className="dot" />
                  {c.label}
                </label>
              ))}
            </div>
          </Field>

          <Field label="Reason (written to the audit log)">
            <input
              className="input"
              placeholder="Required"
              aria-label="Reason"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
            />
          </Field>

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
