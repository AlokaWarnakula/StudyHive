import { useNavigate, useParams } from "react-router-dom";
import { Screen } from "../../components/AppShell";
import { FixtureNotice, KeyValue, Meter, NotBuiltYet, TagOf, Tile } from "../../components/ui";
import { useFixture } from "../../dev/useFixture";

/**
 * W-05 · Quotation detail — GET /api/quotations/{id}: line items, totals, budget comparison and
 * revision history. Owned by S4.
 */
export function QuotationDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const fixture = useFixture((f) => f.quotation);

  if (!fixture.enabled) {
    return (
      <Screen title={`Quotation ${id ?? ""}`} onBack={() => navigate(-1)}>
        <NotBuiltYet owner="S4 costing" what="Quotation detail" />
      </Screen>
    );
  }

  const q = fixture.data;

  return (
    <Screen
      title={`Quotation ${id ?? q.id}`}
      crumb={`${q.request} · ${q.version}`}
      onBack={() => navigate(-1)}
      showUser={false}
      actions={
        <>
          <button type="button" className="btn btn-secondary">
            Print
          </button>
          <button type="button" className="btn btn-primary">
            Approve
          </button>
        </>
      }
    >
      <FixtureNotice owner="S4" what="Quotation lines and budget comparison" />

      <div className="split-wide">
        <Tile>
          <div className="k4">
            <div>
              <span className="lbl">Student</span>
              <div>
                <b>{q.student}</b>
              </div>
            </div>
            <div>
              <span className="lbl">Issued</span>
              <div>
                <b>{q.issued}</b>
              </div>
            </div>
            <div>
              <span className="lbl">Valid until</span>
              <div>
                <b>{q.validUntil}</b>
              </div>
            </div>
            <div>
              <span className="lbl">Status</span>
              <div>
                <TagOf tag={q.status} />
              </div>
            </div>
          </div>

          <hr className="hr" />

          <div className="table-scroll">
            <table className="table">
              <thead>
                <tr>
                  <th>#</th>
                  <th>Description</th>
                  <th>Source</th>
                  <th>Qty</th>
                  <th>Unit</th>
                  <th style={{ textAlign: "right" }}>Amount</th>
                </tr>
              </thead>
              <tbody>
                {q.lines.map((l) => (
                  <tr key={l.no}>
                    <td>{l.no}</td>
                    <td>{l.description}</td>
                    <td>{l.source}</td>
                    <td>{l.qty}</td>
                    <td>{l.unit}</td>
                    <td style={{ textAlign: "right" }}>{l.amount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div style={{ marginLeft: "auto", width: 280, display: "flex", flexDirection: "column", gap: 6 }}>
            <KeyValue label="Subtotal">{q.subtotal}</KeyValue>
            <KeyValue label="Discount">{q.discount}</KeyValue>
            <div className="kv" style={{ borderTop: "1px solid var(--color-divider)", paddingTop: 8 }}>
              <b>Total</b>
              <span className="big" style={{ fontSize: 24 }}>
                {q.total}
              </span>
            </div>
          </div>
        </Tile>

        <div className="stack">
          <Tile label="Against budget">
            <KeyValue label="Student budget">{q.studentBudget}</KeyValue>
            <Meter percent={q.studentBudgetPercent} />
            <span className="fnote">{q.studentBudgetNote}</span>
            <hr className="hr" />
            <KeyValue label="Department budget (Aug)">{q.departmentBudget}</KeyValue>
            <Meter percent={q.departmentBudgetPercent} />
            <span className="fnote">{q.departmentBudgetNote}</span>
          </Tile>

          <Tile label="Versions">
            {q.versions.map((v, i) => (
              <div key={v.label}>
                {i > 0 && <hr className="hr" />}
                <div className="kv">
                  <span>
                    <b>{v.label}</b> · {v.total}
                  </span>
                  <TagOf tag={v.tag} />
                </div>
                <span className="fnote">{v.note}</span>
              </div>
            ))}
          </Tile>
        </div>
      </div>
    </Screen>
  );
}
