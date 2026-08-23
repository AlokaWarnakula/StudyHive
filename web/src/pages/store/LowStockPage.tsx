import { Screen } from "../../components/AppShell";
import { FixtureNotice, KeyValue, MetricTiles, NotBuiltYet, Tag, Tile } from "../../components/ui";
import { useFixture } from "../../dev/useFixture";

/**
 * W-21 · Low stock alerts — GET /api/consumables?below_reorder=true, with the Brevo email to the
 * store officer. Owned by S3.
 */
export function LowStockPage() {
  const fixture = useFixture((f) => f.lowStock);

  if (!fixture.enabled) {
    return (
      <Screen title="Low stock">
        <NotBuiltYet owner="S3 store" what="Low stock alerts" />
      </Screen>
    );
  }

  const l = fixture.data;

  return (
    <Screen
      title="Low stock"
      crumb={`${l.rows.length} items at or below their reorder level`}
      showUser={false}
      actions={
        <button type="button" className="btn btn-primary">
          Email supplier list
        </button>
      }
    >
      <FixtureNotice owner="S3" what="Low-stock items and suggested orders" />

      <MetricTiles metrics={l.metrics} columns={3} />

      <div className="table-scroll">
        <table className="table">
          <thead>
            <tr>
              <th>Item</th>
              <th>In stock</th>
              <th>Reorder at</th>
              <th>Shortfall</th>
              <th>Suggested order</th>
              <th>Supplier</th>
              <th>Lead time</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {l.rows.map((r) => (
              <tr key={r.code}>
                <td>
                  <b>{r.name}</b>
                  <div className="fnote">{r.code}</div>
                </td>
                <td>
                  <b style={r.urgent ? { color: "var(--color-accent-700)" } : undefined}>{r.inStock}</b>
                </td>
                <td>{r.reorderAt}</td>
                <td>{r.shortfall}</td>
                <td>{r.suggested}</td>
                <td>{r.supplier}</td>
                <td>{r.leadTime}</td>
                <td>
                  {/* The out-of-stock item is the one to act on first, so only it gets the
                      primary button — the rest stay secondary. */}
                  <button type="button" className={r.urgent ? "btn btn-primary" : "btn btn-secondary"}>
                    Order
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <Tile label="Requests blocked by stock">
        {l.blocked.map((b) => (
          <KeyValue key={b} label={b}>
            <Tag tone="outline">Waiting</Tag>
          </KeyValue>
        ))}
      </Tile>
    </Screen>
  );
}
