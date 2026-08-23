import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Screen } from "../../components/AppShell";
import { Icon } from "../../components/Icon";
import { FixtureNotice, NotBuiltYet, Pagination, Select, TagOf, Toolbar } from "../../components/ui";
import { useFixture } from "../../dev/useFixture";

/**
 * W-19 · Consumables — GET /api/consumables with a stock-level filter, low-stock highlighting,
 * sort and pagination. Owned by S3.
 */
export function ConsumablesPage() {
  const fixture = useFixture((f) => ({ items: f.consumables, filters: f.filters }));
  const navigate = useNavigate();
  const [search, setSearch] = useState("");

  if (!fixture.enabled) {
    return (
      <Screen title="Consumables">
        <NotBuiltYet owner="S3 store" what="The consumables list" />
      </Screen>
    );
  }

  const items = fixture.data.items;
  const filters = fixture.data.filters;
  const term = search.trim().toLowerCase();
  const rows = term ? items.filter((r) => `${r.name} ${r.code}`.toLowerCase().includes(term)) : items;

  return (
    <Screen
      title="Consumables"
      crumb={`${items.length} shown of 24 items`}
      showUser={false}
      actions={
        <>
          <button type="button" className="btn btn-secondary" onClick={() => navigate("/consumables/CN-04")}>
            <Icon name="download" size={16} />
            Stock in
          </button>
          <button type="button" className="btn btn-primary">
            <Icon name="plus" size={16} />
            Add item
          </button>
        </>
      }
    >
      <FixtureNotice owner="S3" what="Stock levels" />

      <Toolbar>
        <input
          className="input"
          style={{ maxWidth: 250 }}
          placeholder="Search item or code"
          aria-label="Search item or code"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <Select label="Stock level" options={["All", "Healthy", "Low", "Out of stock"]} width={180} />
        <Select label="Supplier" options={filters.suppliers} width={180} />
        <Select label="Sort" options={["Lowest stock", "Name", "Highest value"]} width={180} />
      </Toolbar>

      <div className="table-scroll">
        <table className="table">
          <thead>
            <tr>
              <th>Item</th>
              <th>Code</th>
              <th>Unit price</th>
              <th>In stock</th>
              <th>Reserved</th>
              <th>Free</th>
              <th>Reorder at</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.code} className="row-click" onClick={() => navigate(`/consumables/${r.code}`)}>
                <td>
                  <b>{r.name}</b>
                </td>
                <td>{r.code}</td>
                <td>{r.unitPrice}</td>
                {/* An item at zero is the whole point of this screen, so it is called out in the
                    accent colour rather than left to the status tag alone. */}
                <td style={r.inStock === "0" ? { color: "var(--color-accent-700)", fontWeight: 500 } : undefined}>
                  {r.inStock}
                </td>
                <td>{r.reserved}</td>
                <td>{r.free}</td>
                <td>{r.reorderAt}</td>
                <td>
                  <TagOf tag={r.status} />
                </td>
                <td>
                  <Link to={`/consumables/${r.code}`}>Open</Link>
                </td>
              </tr>
            ))}
            {rows.length === 0 && (
              <tr>
                <td colSpan={9}>
                  <div className="state-view">No item matches “{search}”.</div>
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <Pagination showing={`Showing 1–${rows.length} of 24`} disablePrevious />
    </Screen>
  );
}
