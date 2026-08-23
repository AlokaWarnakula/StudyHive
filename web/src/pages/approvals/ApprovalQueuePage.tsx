import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Screen } from "../../components/AppShell";
import { FixtureNotice, NotBuiltYet, Select, Tag, TagOf, Toolbar } from "../../components/ui";
import { useFixture } from "../../dev/useFixture";

/**
 * W-03 · Approval queue — GET /api/approvals?status=Pending, with search, filter, sort and
 * bulk selection. Owned by S4.
 */
export function ApprovalQueuePage() {
  const fixture = useFixture((f) => f.approvals);
  const navigate = useNavigate();
  const [search, setSearch] = useState("");
  const [selected, setSelected] = useState<string[]>([]);

  if (!fixture.enabled) {
    return (
      <Screen title="Approvals" crumb="Waiting for a decision">
        <NotBuiltYet owner="S4 approvals" what="The approval queue" />
      </Screen>
    );
  }

  const term = search.trim().toLowerCase();
  const rows = term
    ? fixture.data.filter((r) => `${r.student} ${r.purpose} ${r.id}`.toLowerCase().includes(term))
    : fixture.data;

  const allSelected = rows.length > 0 && rows.every((r) => selected.includes(r.id));

  function toggle(id: string) {
    setSelected((s) => (s.includes(id) ? s.filter((x) => x !== id) : [...s, id]));
  }

  return (
    <Screen title="Approvals" crumb={`${fixture.data.length} waiting`}>
      <FixtureNotice owner="S4" what="The approval queue" />

      <Toolbar>
        <input
          className="input"
          style={{ maxWidth: 280 }}
          placeholder="Search student or purpose"
          aria-label="Search student or purpose"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <Select label="Status" options={["Pending", "All", "Over budget", "Revision asked"]} />
        <Select label="Sort" options={["Oldest first", "Newest first", "Highest total"]} />
        <button type="button" className="btn btn-secondary" style={{ marginLeft: "auto" }}>
          Export CSV
        </button>
      </Toolbar>

      <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
        <Tag tone="accent">Pending 6</Tag>
        <Tag tone="outline">Over budget 2</Tag>
        <Tag tone="outline">Revision asked 1</Tag>
      </div>

      <div className="table-scroll">
        <table className="table">
          <thead>
            <tr>
              <th style={{ width: 34 }}>
                <input
                  type="checkbox"
                  aria-label="Select all requests"
                  checked={allSelected}
                  onChange={() => setSelected(allSelected ? [] : rows.map((r) => r.id))}
                />
              </th>
              <th>Request</th>
              <th>Student</th>
              <th>Room · time</th>
              <th>Items</th>
              <th>Total</th>
              <th>Budget</th>
              <th>AI checks</th>
              <th>Waiting</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {rows.map((r, i) => (
              <tr key={r.id}>
                <td>
                  <input
                    type="checkbox"
                    aria-label={`Select ${r.id}`}
                    checked={selected.includes(r.id)}
                    onChange={() => toggle(r.id)}
                  />
                </td>
                <td>
                  <b>{r.id}</b>
                  <div className="fnote">{r.purpose}</div>
                </td>
                <td>
                  {r.student}
                  {r.studentNote && <div className="fnote">{r.studentNote}</div>}
                </td>
                <td>
                  {r.room}
                  <div className="fnote">{r.slot}</div>
                </td>
                <td>{r.items}</td>
                <td>
                  <b>{r.total}</b>
                </td>
                <td>
                  <TagOf tag={r.budget} />
                </td>
                <td>
                  <TagOf tag={r.aiChecks} />
                </td>
                <td>{r.waiting}</td>
                <td>
                  {/* The oldest request is the one the queue wants decided, so it gets the primary
                      button — exactly as the reference frame shows. */}
                  <button
                    type="button"
                    className={i === 0 ? "btn btn-primary" : "btn btn-secondary"}
                    onClick={() => navigate(`/approvals/${r.id}`)}
                  >
                    Review
                  </button>
                </td>
              </tr>
            ))}
            {rows.length === 0 && (
              <tr>
                <td colSpan={10}>
                  <div className="state-view">No request matches “{search}”.</div>
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <div className="bar" style={{ borderTop: "1px solid var(--color-divider)", paddingTop: 12 }}>
        <span className="fnote">{selected.length} selected</span>
        <button type="button" className="btn btn-primary" disabled={selected.length === 0}>
          Approve selected
        </button>
        <button type="button" className="btn btn-secondary" disabled={selected.length === 0}>
          Reject selected
        </button>
        <span className="fnote" style={{ marginLeft: "auto" }}>
          Showing 1–{rows.length} of {fixture.data.length}
        </span>
      </div>
    </Screen>
  );
}
