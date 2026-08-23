import { useState } from "react";
import { Screen } from "../../components/AppShell";
import { Icon } from "../../components/Icon";
import {
  FixtureNotice,
  KeyValue,
  NotBuiltYet,
  Pagination,
  Select,
  TagOf,
  TextField,
  Tile,
  Toolbar,
} from "../../components/ui";
import { useFixture } from "../../dev/useFixture";

/**
 * W-23 · Suppliers — GET / POST / PUT /api/suppliers: items supplied, lead time and contact.
 * A row selects the supplier the side panel edits. Owned by S3.
 */
export function SuppliersPage() {
  const fixture = useFixture((f) => f.suppliers);
  const [search, setSearch] = useState("");
  const [selectedName, setSelectedName] = useState<string | null>(null);

  if (!fixture.enabled) {
    return (
      <Screen title="Suppliers">
        <NotBuiltYet owner="S3 store" what="Suppliers" />
      </Screen>
    );
  }

  const s = fixture.data;
  const term = search.trim().toLowerCase();
  const rows = term ? s.rows.filter((r) => r.name.toLowerCase().includes(term)) : s.rows;
  const selected = s.rows.find((r) => r.name === selectedName) ?? s.rows[0];

  return (
    <Screen
      title="Suppliers"
      crumb={`${s.total} active`}
      showUser={false}
      actions={
        <button type="button" className="btn btn-primary">
          <Icon name="plus" size={16} />
          Add supplier
        </button>
      }
    >
      <FixtureNotice owner="S3" what="Suppliers and their price lists" />

      <div className="split" style={{ gridTemplateColumns: "1.4fr 340px" }}>
        <div className="stack">
          <Toolbar>
            <input
              className="input"
              style={{ maxWidth: 250 }}
              placeholder="Search supplier"
              aria-label="Search supplier"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
            <Select label="Status" options={["Active", "All", "Inactive"]} />
          </Toolbar>

          <div className="table-scroll">
            <table className="table">
              <thead>
                <tr>
                  <th>Supplier</th>
                  <th>Contact</th>
                  <th>Phone</th>
                  <th>Items</th>
                  <th>Lead time</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((r) => (
                  <tr
                    key={r.name}
                    className="row-click"
                    onClick={() => setSelectedName(r.name)}
                    style={selected?.name === r.name ? { background: "var(--color-accent-100)" } : undefined}
                  >
                    <td>
                      <b>{r.name}</b>
                    </td>
                    <td>{r.contact}</td>
                    <td>{r.phone}</td>
                    <td>{r.items}</td>
                    <td>{r.leadTime}</td>
                    <td>
                      <TagOf tag={r.status} />
                    </td>
                  </tr>
                ))}
                {rows.length === 0 && (
                  <tr>
                    <td colSpan={6}>
                      <div className="state-view">No supplier matches “{search}”.</div>
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          <Pagination showing={`Showing 1–${rows.length} of ${s.total}`} disablePrevious />
        </div>

        {selected && (
          <Tile label={selected.name} accented key={selected.name}>
            <TextField label="Contact person" defaultValue={selected.contact} />
            <TextField label="Phone" defaultValue={selected.phone} />
            <TextField label="Email" defaultValue={s.selectedEmail} />
            <TextField label="Lead time (days)" defaultValue={selected.leadTime.replace(/\D/g, "")} />
            <hr className="hr" />
            <span className="lbl">Items supplied</span>
            {s.selectedItems.map((i) => (
              <KeyValue key={i.name} label={i.name}>
                {i.price}
              </KeyValue>
            ))}
            <button type="button" className="btn btn-primary btn-block">
              Save supplier
            </button>
          </Tile>
        )}
      </div>
    </Screen>
  );
}
