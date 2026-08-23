import { Link, useNavigate, useParams } from "react-router-dom";
import { Screen } from "../../components/AppShell";
import { FixtureNotice, KeyValue, NotBuiltYet, Tag, TagOf, TextField, Tile } from "../../components/ui";
import { useFixture } from "../../dev/useFixture";

/**
 * W-04 · Review proposal — approve, reject or ask for a change.
 * POST /api/approvals/{id}/decision: one transaction locks the room and reserves the stock.
 */
export function ReviewProposalPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const fixture = useFixture((f) => f.proposal);

  if (!fixture.enabled) {
    return (
      <Screen title={id ?? "Request"} crumb="Approvals" onBack={() => navigate("/approvals")}>
        <NotBuiltYet owner="S4 approvals" what="The proposal review" />
      </Screen>
    );
  }

  const p = fixture.data;

  return (
    <Screen
      title={`${id ?? p.requestId} · ${p.purpose}`}
      crumb={`Approvals / ${id ?? p.requestId}`}
      onBack={() => navigate("/approvals")}
      actions={<Tag tone="outline">Pending approval</Tag>}
      showUser={false}
    >
      <FixtureNotice owner="S4" what="The proposal, quotation and validation checks" />

      <div className="split">
        <div className="stack">
          <Tile label="What the student asked for">
            <p style={{ margin: 0, fontSize: 15 }}>“{p.quote}”</p>
            <div className="k4" style={{ marginTop: 6 }}>
              <div>
                <span className="lbl">Student</span>
                <div>
                  <b>{p.student}</b>
                </div>
                <span className="fnote">{p.studentNote}</span>
              </div>
              <div>
                <span className="lbl">People</span>
                <div>
                  <b>{p.people}</b>
                </div>
              </div>
              <div>
                <span className="lbl">When</span>
                <div>
                  <b>{p.when}</b>
                </div>
              </div>
              <div>
                <span className="lbl">Their budget</span>
                <div>
                  <b>{p.budget}</b>
                </div>
              </div>
            </div>
          </Tile>

          <Tile label="What the AI proposes" action={<Link to={`/workflows/${p.workflowId}`}>See workflow steps</Link>}>
            <div className="k3">
              <div>
                <span className="lbl">Room</span>
                <div className="big" style={{ fontSize: 22 }}>
                  {p.room.code}
                </div>
                <span className="fnote">{p.room.note}</span>
              </div>
              <div>
                <span className="lbl">No clashes</span>
                <div className="big" style={{ fontSize: 22 }}>
                  {p.clashes.value}
                </div>
                <span className="fnote">{p.clashes.note}</span>
              </div>
              <div>
                <span className="lbl">Items reserved</span>
                <div className="big" style={{ fontSize: 22 }}>
                  {p.reserved.value}
                </div>
                <span className="fnote">{p.reserved.note}</span>
              </div>
            </div>
          </Tile>

          <Tile label="Quotation">
            <div className="table-scroll">
              <table className="table">
                <thead>
                  <tr>
                    <th>Line</th>
                    <th>Detail</th>
                    <th>Qty</th>
                    <th>Rate</th>
                    <th style={{ textAlign: "right" }}>Amount</th>
                  </tr>
                </thead>
                <tbody>
                  {p.lines.map((l) => (
                    <tr key={l.no}>
                      <td>{l.source}</td>
                      <td>{l.description}</td>
                      <td>{l.qty}</td>
                      <td>{l.unit}</td>
                      <td style={{ textAlign: "right" }}>{l.amount}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="kv kv-rule" style={{ borderBottom: 0, borderTop: "1px solid var(--color-divider)", paddingTop: 10 }}>
              <b>Total</b>
              <span className="big" style={{ fontSize: 24 }}>
                {p.total}
              </span>
            </div>
            <div className="kv">
              <span className="fnote">Student budget {p.budget}</span>
              <Tag tone="accent">{p.budgetVerdict}</Tag>
            </div>
          </Tile>
        </div>

        <div className="stack">
          <Tile label="Validation checks">
            {p.checks.map((c) => (
              <KeyValue key={c.label} label={c.label}>
                <TagOf tag={c.tag} />
              </KeyValue>
            ))}
          </Tile>

          <Tile label="Your decision">
            <TextField label="Comment to the student (optional)" placeholder="Confirmed, keep the room tidy." textarea />
            <button type="button" className="btn btn-primary btn-block" style={{ padding: 12 }}>
              Approve booking
            </button>
            <button type="button" className="btn btn-secondary btn-block" style={{ padding: 12 }}>
              Ask for a change
            </button>
            <button type="button" className="btn btn-secondary btn-block" style={{ padding: 12 }}>
              Reject
            </button>
            <p className="fnote" style={{ margin: "8px 0 0" }}>
              Approving locks room {p.room.code} and reserves the stock in one step. The student is emailed and the app
              updates immediately.
            </p>
          </Tile>
        </div>
      </div>
    </Screen>
  );
}
