import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
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
import { useAuthStore } from "../../store/authStore";
import { statusLabel, statusTone } from "./status";

const ACTIVE_WORKFLOW_STATUSES = new Set(["Started", "InProgress"]);
const POLL_INTERVAL_MS = 3000;

/** The lifecycle the reference draws as a five-dot timeline, and where a status sits on it. */
const LIFECYCLE: { title: string; reached: BookingRequestStatus[] }[] = [
  { title: "Draft created", reached: ["Draft"] },
  { title: "Submitted", reached: ["Submitted"] },
  { title: "Processing", reached: ["Processing"] },
  { title: "Pending approval", reached: ["PendingApproval", "RevisionRequested"] },
  { title: "Approved / rejected", reached: ["Approved", "Rejected", "Completed", "Cancelled", "Failed"] },
];

function lifecycleSteps(request: BookingRequest): TimelineStep[] {
  const currentIndex = LIFECYCLE.findIndex((s) => s.reached.includes(request.status));
  return LIFECYCLE.map((s, i) => ({
    title: s.title,
    detail: i === currentIndex ? `Now · ${statusLabel(request.status)}` : undefined,
    state: i < currentIndex ? "done" : i === currentIndex ? "current" : "waiting",
  }));
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
  const [loading, setLoading] = useState(true);

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
        if (!cancelled) setError(err instanceof ApiError ? err.message : "Failed to load this request.");
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

  if (!request) {
    return (
      <Screen title={title} crumb="Requests" onBack={() => navigate("/requests")}>
        <div className="state-view">Request not found.</div>
      </Screen>
    );
  }

  return (
    <Screen
      title={title}
      crumb={`Requests / ${title}`}
      onBack={() => navigate("/requests")}
      showUser={false}
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
                    {request.preferredTimeFrom} – {request.preferredTimeTo}
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
                        <td>{item.consumableId}</td>
                        <td>{item.quantity}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            {/* Stock levels and reservation state live behind S3's consumables API, which does not
                exist yet — the ids the request actually carries are shown instead of inventing them. */}
            <span className="fnote">
              Stock levels and reservation state come from the S3 consumables API, which is not built yet.
            </span>
          </Tile>

          <Tile label="Status timeline">
            <Timeline steps={lifecycleSteps(request)} />
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
                <KeyValue label="Started">{new Date(workflow.startedAt).toLocaleString()}</KeyValue>
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
