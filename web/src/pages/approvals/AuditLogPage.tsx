import { useState } from "react";
import { Screen } from "../../components/AppShell";
import { FixtureNotice, NotBuiltYet, Pagination, Select, TagOf, Toolbar } from "../../components/ui";
import { useFixture } from "../../dev/useFixture";

/**
 * W-08 · Audit log — GET /api/audit-logs. Append-only, searchable and filterable by action,
 * entity, user and date. Owned by S4.
 */
export function AuditLogPage() {
  const fixture = useFixture((f) => ({ log: f.audit, filters: f.filters }));
  const [search, setSearch] = useState("");

  if (!fixture.enabled) {
    return (
      <Screen title="Audit log" crumb="Read-only record of every change">
        <NotBuiltYet owner="S4 audit" what="The audit log" />
      </Screen>
    );
  }

  const a = fixture.data.log;
  const filters = fixture.data.filters;
  const term = search.trim().toLowerCase();
  const rows = term ? a.rows.filter((r) => `${r.entity} ${r.user}`.toLowerCase().includes(term)) : a.rows;

  return (
    <Screen
      title="Audit log"
      crumb="Read-only record of every change"
      actions={
        <button type="button" className="btn btn-secondary">
          Export CSV
        </button>
      }
      showUser={false}
    >
      <FixtureNotice owner="S4" what="Audit rows" />

      <Toolbar>
        <input
          className="input"
          style={{ maxWidth: 260 }}
          placeholder="Search entity id or user"
          aria-label="Search entity id or user"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <Select label="Action" options={["All", "Approve", "Reject", "Create", "Update role"]} width={160} />
        <Select label="Entity" options={filters.auditEntities} width={160} />
        <Select label="User" options={filters.auditUsers} width={160} />
        <input className="input" style={{ maxWidth: 170 }} defaultValue={filters.auditRange} aria-label="Date range" />
      </Toolbar>

      <div className="table-scroll">
        <table className="table">
          <thead>
            <tr>
              <th>When</th>
              <th>User</th>
              <th>Role</th>
              <th>Action</th>
              <th>Entity</th>
              <th>Before → after</th>
              <th>IP</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r, i) => (
              <tr key={`${r.when}-${r.entity}-${i}`}>
                <td>{r.when}</td>
                <td>{r.user}</td>
                <td>{r.role}</td>
                <td>
                  <TagOf tag={r.action} />
                </td>
                <td>{r.entity}</td>
                <td>{r.change}</td>
                <td>{r.ip}</td>
              </tr>
            ))}
            {rows.length === 0 && (
              <tr>
                <td colSpan={7}>
                  <div className="state-view">No audit row matches “{search}”.</div>
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <Pagination showing={`Showing 1–${rows.length} of ${a.total}`} disablePrevious />
    </Screen>
  );
}
