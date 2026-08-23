import { useState } from "react";
import { Screen } from "../../components/AppShell";
import { FixtureNotice, NotBuiltYet, Pagination, Select, TagOf, Toolbar } from "../../components/ui";
import { useFixture } from "../../dev/useFixture";

/**
 * W-22 · Stock reservations — GET /api/stock-reservations?status=.
 * The lifecycle is held → confirmed → issued → released. Owned by S3.
 */
export function ReservationsPage() {
  const fixture = useFixture((f) => ({ reservations: f.reservations, filters: f.filters }));
  const [search, setSearch] = useState("");

  if (!fixture.enabled) {
    return (
      <Screen title="Stock reservations">
        <NotBuiltYet owner="S3 store" what="Stock reservations" />
      </Screen>
    );
  }

  const r = fixture.data.reservations;
  const filters = fixture.data.filters;
  const term = search.trim().toLowerCase();
  const rows = term ? r.rows.filter((x) => `${x.id} ${x.request} ${x.item}`.toLowerCase().includes(term)) : r.rows;

  return (
    <Screen title="Stock reservations" crumb={`${r.open} open`}>
      <FixtureNotice owner="S3" what="Reservations" />

      <Toolbar>
        <input
          className="input"
          style={{ maxWidth: 240 }}
          placeholder="Search request or item"
          aria-label="Search request or item"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <Select label="Status" options={["All", "Held", "Confirmed", "Issued", "Released"]} />
        <Select label="Item" options={filters.consumableItems} />
        <input className="input" style={{ maxWidth: 160 }} defaultValue="Today" aria-label="Date range" />
      </Toolbar>

      <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
        {r.counts.map((c) => (
          <TagOf key={c.label} tag={c} />
        ))}
      </div>

      <div className="table-scroll">
        <table className="table">
          <thead>
            <tr>
              <th>Reservation</th>
              <th>Request</th>
              <th>Student</th>
              <th>Item</th>
              <th>Qty</th>
              <th>Held until</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {rows.map((x) => (
              <tr key={x.id}>
                <td>
                  <b>{x.id}</b>
                </td>
                <td>{x.request}</td>
                <td>{x.student}</td>
                <td>{x.item}</td>
                <td>{x.qty}</td>
                <td>{x.heldUntil}</td>
                <td>
                  <TagOf tag={x.status} />
                </td>
                <td>
                  {x.action === "issue" ? (
                    <button type="button" className="btn btn-secondary">
                      Issue
                    </button>
                  ) : (
                    <button type="button" className="btn btn-ghost">
                      View
                    </button>
                  )}
                </td>
              </tr>
            ))}
            {rows.length === 0 && (
              <tr>
                <td colSpan={8}>
                  <div className="state-view">No reservation matches “{search}”.</div>
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <Pagination showing={`Showing 1–${rows.length} of ${r.open}`} disablePrevious />
    </Screen>
  );
}
