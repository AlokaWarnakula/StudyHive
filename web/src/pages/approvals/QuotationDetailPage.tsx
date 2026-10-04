import { Link, useNavigate, useParams } from "react-router-dom";
import { getQuotation, listQuotations } from "../../api/approvals";
import { Screen } from "../../components/AppShell";
import { KeyValue, Meter, Tag, Tile } from "../../components/ui";
import { useAuthStore } from "../../store/authStore";
import { formatDateTime, formatMoney, formatQuantity, humanize, statusTone, useLoad } from "./s4";

/**
 * W-05 · Quotation detail — GET /api/quotations/{id}: line items, totals and the budget
 * comparison, plus every version quoted for the same request (GET /api/quotations?bookingRequestId).
 */
export function QuotationDetailPage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const token = useAuthStore((s) => s.accessToken);

  const loaded = useLoad(
    () => {
      if (!token) return null;
      return (async () => {
        const quotation = await getQuotation(token, id);
        const versions = await listQuotations(token, { bookingRequestId: quotation.bookingRequestId, pageSize: 100 })
          .then((page) => page.items)
          .catch(() => []);
        return { quotation, versions };
      })();
    },
    id,
    "Failed to load the quotation.",
  );

  const q = loaded.data?.quotation;
  const versions = loaded.data?.versions ?? [];
  const usedPercent = q && q.budgetSnapshot > 0 ? (q.totalAmount / q.budgetSnapshot) * 100 : 0;

  return (
    <Screen
      title={`Quotation ${id.slice(0, 8)}`}
      crumb={q ? `Version ${q.version} · ${formatDateTime(q.createdAt)}` : undefined}
      onBack={() => navigate(-1)}
      actions={
        q?.status === "Proposed" && (
          <Link className="btn btn-primary" to={`/approvals/${q.id}`}>
            Review for approval
          </Link>
        )
      }
    >
      {loaded.error && <p role="alert" className="form-error">{loaded.error}</p>}
      {loaded.loading && !q && <div className="state-view">Loading…</div>}

      {q && (
        <div className="split-wide">
          <Tile>
            <div className="k4">
              <div>
                <span className="lbl">Status</span>
                <div><Tag tone={statusTone(q.status)}>{humanize(q.status)}</Tag></div>
              </div>
              <div>
                <span className="lbl">Version</span>
                <div><b>{q.version}</b></div>
              </div>
              <div>
                <span className="lbl">Issued</span>
                <div><b>{formatDateTime(q.createdAt)}</b></div>
              </div>
              <div>
                <span className="lbl">Updated</span>
                <div><b>{formatDateTime(q.updatedAt)}</b></div>
              </div>
            </div>

            <hr className="hr" />

            <div className="table-scroll">
              <table className="table">
                <thead>
                  <tr>
                    <th>#</th>
                    <th>Item</th>
                    <th>Type</th>
                    <th>Qty</th>
                    <th>Rate</th>
                    <th style={{ textAlign: "right" }}>Amount</th>
                  </tr>
                </thead>
                <tbody>
                  {q.lineItems.map((line, index) => (
                    <tr key={line.id}>
                      <td>{index + 1}</td>
                      <td>
                        {line.itemName}
                        {line.itemType === "Room" && (
                          <div className="fnote">{line.roomBookingId ? "Room booked" : "Not booked until approved"}</div>
                        )}
                      </td>
                      <td>{line.itemType}</td>
                      <td>{formatQuantity(line.itemType, line.quantity)}</td>
                      <td>{formatMoney(line.unitPrice, q.currency)}</td>
                      <td style={{ textAlign: "right" }}>{formatMoney(line.lineTotal, q.currency)}</td>
                    </tr>
                  ))}
                  {q.lineItems.length === 0 && (
                    <tr>
                      <td colSpan={6}><div className="state-view">This quotation has no lines.</div></td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>

            <div style={{ marginLeft: "auto", width: 280, display: "flex", flexDirection: "column", gap: 6 }}>
              <KeyValue label="Room fee">{formatMoney(q.roomFee, q.currency)}</KeyValue>
              <KeyValue label="Items">{formatMoney(q.consumableCost, q.currency)}</KeyValue>
              <div className="kv" style={{ borderTop: "1px solid var(--color-divider)", paddingTop: 8 }}>
                <b>Total</b>
                <span className="big" style={{ fontSize: 24 }}>{formatMoney(q.totalAmount, q.currency)}</span>
              </div>
            </div>
          </Tile>

          <div className="stack">
            <Tile label="Against budget">
              <KeyValue label="Student budget">{formatMoney(q.budgetSnapshot, q.currency)}</KeyValue>
              <Meter percent={Math.min(usedPercent, 100)} />
              <span className="fnote">
                {q.withinBudget
                  ? `${usedPercent.toFixed(0)}% of the budget`
                  : `Over budget by ${formatMoney(q.totalAmount - q.budgetSnapshot, q.currency)}`}
              </span>
            </Tile>

            <Tile label="Versions">
              {versions.length === 0 ? (
                <span className="fnote">No other versions.</span>
              ) : (
                versions.map((v, index) => (
                  <div key={v.id}>
                    {index > 0 && <hr className="hr" />}
                    <div className="kv">
                      <span>
                        {v.id === q.id ? <b>Version {v.version}</b> : <Link to={`/quotations/${v.id}`}>Version {v.version}</Link>}
                        {" · "}
                        {formatMoney(v.totalAmount, v.currency)}
                      </span>
                      <Tag tone={statusTone(v.status)}>{humanize(v.status)}</Tag>
                    </div>
                    <span className="fnote">{formatDateTime(v.createdAt)}</span>
                  </div>
                ))
              )}
            </Tile>
          </div>
        </div>
      )}
    </Screen>
  );
}
