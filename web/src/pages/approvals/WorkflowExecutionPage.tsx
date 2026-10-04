import { useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { getWorkflowExecution, type WorkflowStep } from "../../api/approvals";
import { Screen } from "../../components/AppShell";
import { KeyValue, Tag, Tile } from "../../components/ui";
import { useAuthStore } from "../../store/authStore";
import { formatDateTime, formatPayload, humanize, statusTone, useLoad } from "./s4";

/**
 * W-06 · Workflow execution viewer — GET /api/workflow-executions/{id}: the run, its plan, and
 * every step attempt with the tool's own input and output. Payloads are rendered as text, never
 * as markup, so agent or student text in them cannot inject anything.
 */
export function WorkflowExecutionPage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const token = useAuthStore((s) => s.accessToken);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const loaded = useLoad(
    () => (token ? getWorkflowExecution(token, id) : null),
    id,
    "Failed to load the workflow run.",
  );

  const run = loaded.data;
  const steps = run ? [...run.steps].sort((a, b) => a.stepNumber - b.stepNumber || a.attempt - b.attempt) : [];
  const selected = steps.find((s) => s.id === selectedId) ?? steps[steps.length - 1];

  return (
    <Screen
      title={`Workflow ${id.slice(0, 8)}`}
      crumb={run ? run.execution.objective : undefined}
      onBack={() => navigate("/workflows")}
      actions={run && <Tag tone={statusTone(run.execution.status)}>{humanize(run.execution.status)}</Tag>}
    >
      {loaded.error && <p role="alert" className="form-error">{loaded.error}</p>}
      {loaded.loading && !run && <div className="state-view">Loading…</div>}

      {run && (
        <div className="split-wide" style={{ gridTemplateColumns: "1fr 1.3fr" }}>
          <div className="stack">
            <Tile label="Run">
              <KeyValue label="Started">{formatDateTime(run.execution.startedAt)}</KeyValue>
              <KeyValue label="Finished">{formatDateTime(run.execution.completedAt)}</KeyValue>
              <KeyValue label="Step">
                {`${run.execution.currentStep} of ${run.execution.totalSteps ?? "?"}`}
              </KeyValue>
              {run.execution.errorCode && (
                <>
                  <KeyValue label="Error">
                    <Tag tone="outline">{run.execution.errorCode}</Tag>
                  </KeyValue>
                  {run.execution.errorMessage && <p className="form-error" style={{ margin: 0 }}>{run.execution.errorMessage}</p>}
                </>
              )}
            </Tile>

            <Tile label="Steps">
              {steps.length === 0 ? (
                <div className="state-view">No steps have been logged yet.</div>
              ) : (
                <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                  {steps.map((step) => (
                    <button
                      key={step.id}
                      type="button"
                      className={step.id === selected?.id ? "btn btn-primary" : "btn btn-secondary"}
                      style={{ justifyContent: "space-between", display: "flex", textAlign: "left" }}
                      aria-pressed={step.id === selected?.id}
                      onClick={() => setSelectedId(step.id)}
                    >
                      <span>
                        {step.stepNumber}. {step.agentName}
                        {step.attempt > 1 ? ` · attempt ${step.attempt}` : ""}
                      </span>
                      <span>{step.validationResult ?? (step.errorMessage ? "Error" : "")}</span>
                    </button>
                  ))}
                </div>
              )}
            </Tile>

            {run.plan != null && (
              <Tile label="Plan">
                <details>
                  <summary>Show the planner's plan</summary>
                  <pre style={PRE}>{formatPayload(run.plan)}</pre>
                </details>
              </Tile>
            )}
          </div>

          <div className="stack">{selected ? <StepDetail step={selected} /> : null}</div>
        </div>
      )}
    </Screen>
  );
}

function StepDetail({ step }: { step: WorkflowStep }) {
  return (
    <>
      <Tile
        label={`Step ${step.stepNumber} · ${step.agentName}`}
        action={step.validationResult && <Tag tone={statusTone(step.validationResult)}>{step.validationResult}</Tag>}
      >
        <div className="k3">
          <div>
            <span className="lbl">Tool</span>
            <div><b>{step.toolName ?? "—"}</b></div>
          </div>
          <div>
            <span className="lbl">Duration</span>
            <div><b>{step.durationMs != null ? `${step.durationMs} ms` : "—"}</b></div>
          </div>
          <div>
            <span className="lbl">Attempt</span>
            <div><b>{step.attempt}</b></div>
          </div>
        </div>
        {step.validationDetails && <span className="fnote">{step.validationDetails}</span>}
        {step.errorMessage && <p role="alert" className="form-error" style={{ margin: 0 }}>{step.errorMessage}</p>}
      </Tile>

      <Tile>
        <span className="lbl">Input</span>
        <pre style={PRE}>{formatPayload(step.input)}</pre>
        <span className="lbl" style={{ marginTop: 6 }}>Output</span>
        <pre style={PRE}>{formatPayload(step.output)}</pre>
      </Tile>
    </>
  );
}

const PRE: React.CSSProperties = {
  margin: 0,
  fontSize: 12,
  maxHeight: 420,
  background: "var(--color-surface)",
  border: "1px solid var(--color-divider)",
  padding: 10,
  overflow: "auto",
  whiteSpace: "pre-wrap",
  wordBreak: "break-word",
};
