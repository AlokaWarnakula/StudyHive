import { useNavigate, useParams } from "react-router-dom";
import { Screen } from "../../components/AppShell";
import { FixtureNotice, KeyValue, NotBuiltYet, TagOf, Timeline, Tile } from "../../components/ui";
import { useFixture } from "../../dev/useFixture";

/**
 * W-06 · Workflow execution viewer — GET /api/workflow-executions/{id}: every step with the
 * agent's own input and output. Owned by S4.
 */
export function WorkflowExecutionPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const fixture = useFixture((f) => f.workflow);

  if (!fixture.enabled) {
    return (
      <Screen title={`Workflow ${id ?? ""}`} onBack={() => navigate("/workflows")}>
        <NotBuiltYet owner="S4 workflow" what="The execution viewer" />
      </Screen>
    );
  }

  const w = fixture.data;

  return (
    <Screen
      title={`Workflow ${id ?? w.id}`}
      crumb={`${w.request} · ${w.finishedIn}`}
      onBack={() => navigate("/workflows")}
      actions={<TagOf tag={w.status} />}
      showUser={false}
    >
      <FixtureNotice owner="S4" what="Workflow steps and agent payloads" />

      <div className="split-wide" style={{ gridTemplateColumns: "1fr 1.3fr" }}>
        <Tile label="Steps">
          <Timeline steps={w.steps} />
          <hr className="hr" />
          <KeyValue label="Tokens used">{w.tokensUsed}</KeyValue>
          <KeyValue label="Tool calls">{w.toolCalls}</KeyValue>
          <KeyValue label="Retries">{w.retries}</KeyValue>
        </Tile>

        <div className="stack">
          <Tile label={w.selected.title} action={<TagOf tag={w.selected.status} />}>
            <div className="k3">
              <div>
                <span className="lbl">Tools called</span>
                <div>
                  <b>{w.selected.tools}</b>
                </div>
              </div>
              <div>
                <span className="lbl">Duration</span>
                <div>
                  <b>{w.selected.duration}</b>
                </div>
              </div>
              <div>
                <span className="lbl">Model</span>
                <div>
                  <b>{w.selected.model}</b>
                </div>
              </div>
            </div>
          </Tile>

          <Tile>
            <span className="lbl">Input</span>
            <pre style={PRE}>{w.selected.input}</pre>
            <span className="lbl" style={{ marginTop: 6 }}>
              Output
            </span>
            <pre style={PRE}>{w.selected.output}</pre>
          </Tile>
        </div>
      </div>
    </Screen>
  );
}

const PRE: React.CSSProperties = {
  margin: 0,
  fontSize: 12,
  background: "var(--color-surface)",
  border: "1px solid var(--color-divider)",
  padding: 10,
  overflow: "auto",
};
