import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { Screen } from "../../components/AppShell";
import { KeyValue, Tag, Timeline, Tile, type TimelineStep } from "../../components/ui";
import { ApiError } from "../../api/client";
import {
  getBookingRequest,
  getWorkflowStatus,
  type BookingRequest,
  type BookingRequestStatus,
  type WorkflowStatusResponse,
} from "../../api/bookingRequests";
import { getConsumable } from "../../api/consumables";
import { useAuthStore } from "../../store/authStore";
import { statusLabel, statusTone } from "./status";
import { colomboSlot, colomboStamp, colomboTime } from "../../utils/colomboTime";
import type { RoomBookingSummary } from "../../api/bookingRequests";

const ACTIVE_WORKFLOW_STATUSES = new Set(["Started", "InProgress"]);
const POLL_INTERVAL_MS = 3000;

const stamp = colomboStamp;

/** "10:00:00" → "10:00" (CW-11: no seconds). */
function hhmm(time: string): string {
  return time.slice(0, 5);
}

const FINAL_STATUSES = new Set<BookingRequestStatus>(["Approved", "Rejected", "RevisionRequested", "Cancelled", "Failed", "Completed"]);

/**
 * AUDIT CW-10: the request's own history, oldest first — created, the workflow run, the decision
 * or final status, and each room booking with its check-in — instead of a fixed five-dot list.
 */
function historySteps(request: BookingRequest, workflow: WorkflowStatusResponse | null): TimelineStep[] {
  const steps: TimelineStep[] = [{ title: "Request created", detail: stamp(request.createdAt), state: "done" }];
  if (workflow) {
    steps.push({ title: "Sent to the agents", detail: stamp(workflow.startedAt), state: "done" });
    if (ACTIVE_WORKFLOW_STATUSES.has(workflow.status)) {
      steps.push({ title: "Agents working", detail: `Step ${workflow.currentStep}${workflow.totalSteps ? ` of ${workflow.totalSteps}` : ""}`, state: "current" });
    } else if (workflow.completedAt) {
      steps.push({ title: `Agents ${workflow.status === "Failed" ? "failed" : "finished"}`, detail: stamp(workflow.completedAt), state: "done" });
    }
  }
  if (request.status === "PendingApproval") {
    steps.push({ title: "Waiting for a librarian", state: "current" });
  } else if (FINAL_STATUSES.has(request.status)) {
    steps.push({ title: statusLabel(request.status), detail: stamp(request.updatedAt), state: "done" });
  } else if (request.status !== "Draft" || steps.length === 1) {
    steps.push({ title: statusLabel(request.status), state: "current" });
  }
  for (const b of request.roomBookings ?? []) {
    steps.push({ title: `Room booked · ${b.roomName} · ${colomboSlot(b.startsAt, b.endsAt)}`, detail: bookingLabel(b), state: "done" });
    if (b.checkedInAt) steps.push({ title: `Checked in · ${b.roomName} · ${stamp(b.checkedInAt)}`, state: "done" });
  }
  return steps;
}

/**
 * W-11 · Request detail — GET /api/booking-requests/{id} plus its workflow status. A real S1
 * screen: the request, its items and the live workflow steps all come from the API, and the
 * workflow is polled while it is still running.
 */
export function RequestDetailPage() {
  const { id } = useParams<{ id: string }>();
  const token = useAuthStore((s) => s.accessToken);
  const navigate = useNavigate();

  const [request, setRequest] = useState<BookingRequest | null>(null);
  const [workflow, setWorkflow] = useState<WorkflowStatusResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notFound, setNotFound] = useState(false);
  const [loading, setLoading] = useState(true);
  /** CW-10: consumable names for the requested items, by id; an id stays shown if its read fails. */
  const [itemNames, setItemNames] = useState<Record<string, string>>({});

  useEffect(() => {
    if (!token || !id) return;
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout> | undefined;

    async function load() {
      try {
        const [requestData, workflowData] = await Promise.all([
          getBookingRequest(token!, id!),
          getWorkflowStatus(token!, id!).catch((err) =>
            err instanceof ApiError && err.status === 404 ? null : Promise.reject(err),
          ),
        ]);
        if (cancelled) return;
        setRequest(requestData);
        setWorkflow(workflowData);
        setError(null);

        if (workflowData && ACTIVE_WORKFLOW_STATUSES.has(workflowData.status)) {
          timer = setTimeout(load, POLL_INTERVAL_MS);
        }
      } catch (err) {
        if (cancelled) return;
        if (err instanceof ApiError && (err.status === 404 || err.status === 400)) setNotFound(true);
        else setError(err instanceof ApiError ? err.message : "Failed to load this request.");
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    load();
    return () => {
      cancelled = true;
      if (timer) clearTimeout(timer);
    };
  }, [token, id]);

  const itemIds = request ? [...new Set(request.items.map((item) => item.consumableId))].join(",") : "";
  useEffect(() => {
    if (!token || !itemIds) return;
    let cancelled = false;
    Promise.all(itemIds.split(",").map((itemId) =>
      getConsumable(token, itemId).then((c) => [itemId, c.consumable.name] as const).catch(() => null),
    )).then((pairs) => {
      if (!cancelled) setItemNames(Object.fromEntries(pairs.filter((p) => p !== null)));
    });
    return () => { cancelled = true; };
  }, [token, itemIds]);

  const title = request ? request.id.slice(0, 8) : (id ?? "Request");

  if (loading) {
    return (
      <Screen title={title} crumb="Requests" onBack={() => navigate("/requests")}>
        <div className="state-view">Loading…</div>
      </Screen>
    );
  }

  if (error) {
    return (
      <Screen title={title} crumb="Requests" onBack={() => navigate("/requests")}>
        <p role="alert" className="form-error">
          {error}
        </p>
      </Screen>
    );
  }

  if (notFound || !request) {
    return (
      <Screen title="Request not found" crumb="Requests" onBack={() => navigate("/requests")}>
        <div className="state-view" role="alert">
          <p style={{ marginTop: 0 }}>There is no booking request with this id.</p>
          <Link to="/requests" className="btn btn-secondary">Back to booking requests</Link>
        </div>
      </Screen>
    );
  }

  return (
    <Screen
      title={title}
      crumb={`Requests / ${title}`}
      onBack={() => navigate("/requests")}
      actions={<Tag tone={statusTone(request.status)}>{statusLabel(request.status)}</Tag>}
    >
      <div className="split">
        <div className="stack">
          <Tile label="Request">
            <p style={{ margin: 0, fontSize: 15 }}>“{request.objective}”</p>
            <div className="k4">
              <div>
                <span className="lbl">People</span>
                <div>
                  <b>{request.groupSize}</b>
                </div>
              </div>
              <div>
                <span className="lbl">Preferred dates</span>
                <div>
                  <b>
                    {request.preferredDateFrom} – {request.preferredDateTo}
                  </b>
                </div>
              </div>
              <div>
                <span className="lbl">Preferred time</span>
                <div>
                  <b>
                    {hhmm(request.preferredTimeFrom)} – {hhmm(request.preferredTimeTo)}
                  </b>
                </div>
              </div>
              <div>
                <span className="lbl">Budget</span>
                <div>
                  <b>Rs. {request.budget.toFixed(2)}</b>
                </div>
              </div>
            </div>
          </Tile>

          {/* CW-12: where the request was booked and whether the student turned up. */}
          {request.roomBookings && request.roomBookings.length > 0 && (
            <Tile label="Room bookings">
              <div className="table-scroll">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Room</th>
                      <th>When</th>
                      <th>Status</th>
                      <th>Checked in</th>
                    </tr>
                  </thead>
                  <tbody>
                    {request.roomBookings.map((b) => (
                      <tr key={b.id}>
                        <td>
                          <b>{b.roomName}</b>
                        </td>
                        <td>{colomboSlot(b.startsAt, b.endsAt)}</td>
                        <td>
                          <Tag tone={bookingTone(b)}>{bookingLabel(b)}</Tag>
                        </td>
                        <td>{b.checkedInAt ? colomboTime(b.checkedInAt) : "—"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </Tile>
          )}

          <Tile label="Requested items">
            {request.items.length === 0 ? (
              <div className="state-view">No consumables were requested.</div>
            ) : (
              <div className="table-scroll">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Consumable</th>
                      <th>Qty</th>
                    </tr>
                  </thead>
                  <tbody>
                    {request.items.map((item) => (
                      <tr key={item.consumableId}>
                        <td>{itemNames[item.consumableId] ?? item.consumableId}</td>
                        <td>{item.quantity}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Tile>

          <Tile label="Status timeline">
            <Timeline steps={historySteps(request, workflow)} />
          </Tile>
        </div>

        <div className="stack">
          <Tile label="Workflow">
            {!workflow ? (
              <div className="state-view">No workflow has been started for this request yet.</div>
            ) : (
              <>
                <KeyValue label="Workflow">{workflow.workflowId.slice(0, 8)}</KeyValue>
                <KeyValue label="Status">
                  <Tag tone={statusTone(workflow.status)}>{statusLabel(workflow.status)}</Tag>
                </KeyValue>
                <KeyValue label="Step">
                  {workflow.totalSteps ? `${workflow.currentStep} of ${workflow.totalSteps}` : String(workflow.currentStep)}
                </KeyValue>
                <KeyValue label="Started">{colomboStamp(workflow.startedAt)}</KeyValue>
                {workflow.errorCode && (
                  <p role="alert" className="form-error">
                    {workflow.errorCode}: {workflow.errorMessage}
                  </p>
                )}
              </>
            )}
          </Tile>

          {workflow && workflow.steps.length > 0 && (
            <Tile label="Agent steps">
              <Timeline
                steps={workflow.steps.map((step) => ({
                  title: `${step.stepNumber} · ${step.agentName}`,
                  detail: [
                    step.toolName,
                    step.validationResult,
                    step.durationMs != null ? `${(step.durationMs / 1000).toFixed(1)} s` : null,
                    step.errorMessage,
                  ]
                    .filter(Boolean)
                    .join(" · "),
                  state: step.errorMessage ? "current" : "done",
                }))}
              />
              {workflow.steps
                .filter((s) => s.outputJson)
                .map((s) => (
                  <details key={s.stepNumber}>
                    <summary className="fnote">Step {s.stepNumber} output</summary>
                    <pre
                      style={{
                        margin: 0,
                        fontSize: 12,
                        background: "var(--color-surface)",
                        border: "1px solid var(--color-divider)",
                        padding: 10,
                        overflow: "auto",
                      }}
                    >
                      {s.outputJson}
                    </pre>
                  </details>
                ))}
            </Tile>
          )}
        </div>
      </div>
    </Screen>
  );
}

/** CW-12: a booking that was checked in says so, whatever its later status. */
function bookingLabel(b: RoomBookingSummary): string {
  if (b.checkedInAt) return "Checked in";
  return b.status === "NoShow" ? "No-show" : b.status;
}

function bookingTone(b: RoomBookingSummary): "accent" | "outline" | "neutral" {
  if (b.checkedInAt || b.status === "Confirmed") return "accent";
  return b.status === "NoShow" ? "outline" : "neutral";
}
