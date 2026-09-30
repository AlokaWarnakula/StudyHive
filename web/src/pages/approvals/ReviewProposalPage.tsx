import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  approvalConflictKind,
  getApproval,
  getWorkflowExecution,
  submitApprovalDecision,
  type ApprovalConflictKind,
  type ApprovalDecisionKind,
} from "../../api/approvals";
import { getBookingRequest } from "../../api/bookingRequests";
import { ApiError } from "../../api/client";
import { Screen } from "../../components/AppShell";
import { Field, KeyValue, Tag, Tile } from "../../components/ui";
import { useAuthStore } from "../../store/authStore";
import {
  formatDateTime,
  formatMoney,
  formatQuantity,
  humanize,
  readValidation,
  statusTone,
  useLoad,
  type ValidationView,
} from "./s4";

const CONFLICT_TEXT: Record<ApprovalConflictKind, string> = {
  "already-decided": "Someone has already decided this quotation.",
  "room-conflict": "The room is no longer free for this slot. Nothing was booked or reserved.",
  "insufficient-stock": "There is not enough stock left for the requested items. Nothing was booked or reserved.",
  "proposal-incomplete": "The proposal is missing the details needed to book it. Nothing was booked or reserved.",
  conflict: "The approval could not be committed. Nothing was booked or reserved.",
};

const ACTION_LABEL: Record<ApprovalDecisionKind, string> = {
  Approved: "Approve booking",
  RevisionRequested: "Ask for a change",
  Rejected: "Reject",
};

/** A decision's failure as one line for the librarian: 409s by kind, 400s by field, else the message. */
function decisionError(reason: unknown): string {
  const conflict = approvalConflictKind(reason);
  if (conflict && reason instanceof ApiError) {
    return reason.problem.detail ? `${CONFLICT_TEXT[conflict]} ${reason.problem.detail}` : CONFLICT_TEXT[conflict];
  }
  if (reason instanceof ApiError && reason.problem.errors) {
    return Object.values(reason.problem.errors).flat().join(" ");
  }
  return reason instanceof ApiError ? reason.message : "The decision could not be saved.";
}

/**
 * W-04 · Review proposal — the librarian's approve / reject / request-revision decision on one
 * quotation (GET /api/approvals/{quotationId}, POST /api/approvals).
 *
 * The proposed slots and rule results come from the workflow's Validation step: its logged input
 * is the exact proposal the agent priced, and those are the slots an approval books. Approving
 * books the room and reserves the stock in one transaction; a clash comes back as a 409 and
 * nothing is written, which the screen says plainly.
 */
export function ReviewProposalPage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const token = useAuthStore((s) => s.accessToken);
  const [comments, setComments] = useState("");
  const [submitting, setSubmitting] = useState<ApprovalDecisionKind | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  const proposal = useLoad(
    () => {
      if (!token) return null;
      return (async () => {
        const detail = await getApproval(token, id);
        // The request and its workflow add the slots and checks; the decision works without them.
        const request = await getBookingRequest(token, detail.item.bookingRequestId).catch(() => null);
        const workflow = request?.latestWorkflowId
          ? await getWorkflowExecution(token, request.latestWorkflowId).catch(() => null)
          : null;
        return { detail, request, workflow, validation: workflow ? readValidation(workflow.steps) : null };
      })();
    },
    id,
    "Failed to load the proposal.",
  );

  async function decide(decision: ApprovalDecisionKind) {
    if (!token) return;
    const trimmed = comments.trim();
    if (decision !== "Approved" && !trimmed) {
      setFormError("Write a comment for the student before rejecting or asking for a change.");
      return;
    }
    setSubmitting(decision);
    setFormError(null);
    try {
      await submitApprovalDecision(token, { quotationId: id, decision, comments: trimmed || null });
      setComments("");
      proposal.reload();
    } catch (reason) {
      setFormError(decisionError(reason));
      // Someone else decided it meanwhile: show the decision that stands.
      if (approvalConflictKind(reason) === "already-decided") proposal.reload();
    } finally {
      setSubmitting(null);
    }
  }

  const data = proposal.data;
  const item = data?.detail.item;

  return (
    <Screen
      title={item ? item.objective : "Review proposal"}
      crumb={item ? `Approvals / quotation version ${item.version}` : "Approvals"}
      onBack={() => navigate("/approvals")}
      actions={item && <Tag tone={item.status === "Pending" ? "neutral" : statusTone(item.status)}>{humanize(item.status)}</Tag>}
      showUser={false}
    >
      {proposal.error && <p role="alert" className="form-error">{proposal.error}</p>}
      {proposal.loading && !data && <div className="state-view">Loading…</div>}

      {data && item && (
        <div className="split">
          <div className="stack">
            <Tile label="What the student asked for">
              <p style={{ margin: 0, fontSize: 15 }}>“{item.objective}”</p>
              <div className="k4" style={{ marginTop: 6 }}>
                <div>
                  <span className="lbl">People</span>
                  <div><b>{item.groupSize}</b></div>
                </div>
                <div>
                  <span className="lbl">When</span>
                  <div>
                    <b>
                      {data.request
                        ? `${data.request.preferredDateFrom} – ${data.request.preferredDateTo}`
                        : "—"}
                    </b>
                  </div>
                  {data.request && (
                    <span className="fnote">
                      {data.request.preferredTimeFrom.slice(0, 5)}–{data.request.preferredTimeTo.slice(0, 5)} ·{" "}
                      {data.request.sessionsRequired} × {data.request.sessionDurationMinutes} min
                    </span>
                  )}
                </div>
                <div>
                  <span className="lbl">Their budget</span>
                  <div><b>{formatMoney(item.budgetSnapshot, item.currency)}</b></div>
                </div>
                <div>
                  <span className="lbl">Quoted</span>
                  <div><b>{formatDateTime(item.createdAt)}</b></div>
                </div>
              </div>
              {data.request?.notes && <span className="fnote">Note: {data.request.notes}</span>}
            </Tile>

            <ProposedSlots validation={data.validation} workflowId={data.workflow?.execution.id ?? null} />

            <Tile label="Quotation" action={<Link to={`/quotations/${item.quotationId}`}>Open quotation</Link>}>
              <div className="table-scroll">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Type</th>
                      <th>Item</th>
                      <th>Qty</th>
                      <th>Rate</th>
                      <th style={{ textAlign: "right" }}>Amount</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.detail.lineItems.map((line) => (
                      <tr key={line.id}>
                        <td>{line.itemType}</td>
                        <td>{line.itemName}</td>
                        <td>{formatQuantity(line.itemType, line.quantity)}</td>
                        <td>{formatMoney(line.unitPrice, item.currency)}</td>
                        <td style={{ textAlign: "right" }}>{formatMoney(line.lineTotal, item.currency)}</td>
                      </tr>
                    ))}
                    {data.detail.lineItems.length === 0 && (
                      <tr>
                        <td colSpan={5}><div className="state-view">This quotation has no lines.</div></td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
              <KeyValue label="Room fee">{formatMoney(item.roomFee, item.currency)}</KeyValue>
              <KeyValue label="Items">{formatMoney(item.consumableCost, item.currency)}</KeyValue>
              <div className="kv" style={{ borderTop: "1px solid var(--color-divider)", paddingTop: 10 }}>
                <b>Total</b>
                <span className="big" style={{ fontSize: 24 }}>{formatMoney(item.totalAmount, item.currency)}</span>
              </div>
              <div className="kv">
                <span className="fnote">Student budget {formatMoney(item.budgetSnapshot, item.currency)}</span>
                <Tag tone={item.withinBudget ? "accent" : "outline"}>{item.withinBudget ? "Within budget" : "Over budget"}</Tag>
              </div>
            </Tile>
          </div>

          <div className="stack">
            <Tile label="Validation checks">
              {data.validation && data.validation.results.length > 0 ? (
                data.validation.results.map((r) => (
                  <div key={r.rule}>
                    <KeyValue label={humanize(r.rule.replace(/^validate_/, "").replace(/_/g, " "))}>
                      <Tag tone={r.passed ? "accent" : "outline"}>{r.passed ? "Pass" : "Fail"}</Tag>
                    </KeyValue>
                    {r.detail && <span className="fnote">{r.detail}</span>}
                  </div>
                ))
              ) : (
                <div className="state-view">No validation results were logged for this proposal.</div>
              )}
            </Tile>

            {item.status === "Pending" ? (
              <Tile label="Your decision">
                <Field label="Comment to the student (required to reject or ask for a change)">
                  <textarea
                    className="input"
                    aria-label="Comment to the student"
                    placeholder="Confirmed, keep the room tidy."
                    maxLength={2000}
                    value={comments}
                    onChange={(e) => setComments(e.target.value)}
                  />
                </Field>
                {formError && <p role="alert" className="form-error">{formError}</p>}
                {(["Approved", "RevisionRequested", "Rejected"] as const).map((decision) => (
                  <button
                    key={decision}
                    type="button"
                    className={decision === "Approved" ? "btn btn-primary btn-block" : "btn btn-secondary btn-block"}
                    style={{ padding: 12 }}
                    disabled={submitting !== null}
                    onClick={() => decide(decision)}
                  >
                    {submitting === decision ? "Saving…" : ACTION_LABEL[decision]}
                  </button>
                ))}
                <p className="fnote" style={{ margin: "8px 0 0" }}>
                  Approving books the room and reserves the stock in one step. If either is no longer possible, nothing is
                  written and you are told why.
                </p>
              </Tile>
            ) : (
              <Tile label="Decision">
                {formError && <p role="alert" className="form-error">{formError}</p>}
                {item.decision ? (
                  <>
                    <KeyValue label="Outcome">
                      <Tag tone={statusTone(item.decision.decision)}>{humanize(item.decision.decision)}</Tag>
                    </KeyValue>
                    <KeyValue label="Decided">{formatDateTime(item.decision.decidedAt)}</KeyValue>
                    <KeyValue label="By">{item.decision.decidedByRole}</KeyValue>
                    {item.decision.comments && <p style={{ margin: 0 }}>“{item.decision.comments}”</p>}
                  </>
                ) : (
                  <KeyValue label="Quotation">{humanize(item.quotationStatus)}</KeyValue>
                )}
              </Tile>
            )}
          </div>
        </div>
      )}
    </Screen>
  );
}

function ProposedSlots({ validation, workflowId }: { validation: ValidationView | null; workflowId: string | null }) {
  return (
    <Tile label="Proposed slots" action={workflowId && <Link to={`/workflows/${workflowId}`}>See workflow steps</Link>}>
      {validation && validation.slots.length > 0 ? (
        <div className="table-scroll">
          <table className="table">
            <thead>
              <tr>
                <th>Room</th>
                <th>Starts</th>
                <th>Ends</th>
              </tr>
            </thead>
            <tbody>
              {validation.slots.map((slot) => (
                <tr key={`${slot.roomId}-${slot.startsAt}`}>
                  <td>{slot.roomName}</td>
                  <td>{formatDateTime(slot.startsAt)}</td>
                  <td>{formatDateTime(slot.endsAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="state-view">The workflow log for this proposal is not available.</div>
      )}
    </Tile>
  );
}
