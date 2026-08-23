import { useState } from "react";
import { Link } from "react-router-dom";
import { Screen } from "../../components/AppShell";
import { FixtureNotice, MetricTiles, NotBuiltYet, Pagination, Select, TagOf, Toolbar } from "../../components/ui";
import { useFixture } from "../../dev/useFixture";

/**
 * W-07 · Execution history — GET /api/workflow-executions. Failed runs are listed first because
 * that is the reason this screen exists; retry is offered on the ones that can be retried.
 */
export function ExecutionHistoryPage() {
  const fixture = useFixture((f) => f.executions);
  const [search, setSearch] = useState("");

  if (!fixture.enabled) {
    return (
      <Screen title="Workflow runs" crumb="Last 7 days">
        <NotBuiltYet owner="S4 workflow" what="Execution history" />
      </Screen>
    );
  }

  const e = fixture.data;
  const term = search.trim().toLowerCase();
  const runs = term ? e.runs.filter((r) => `${r.id} ${r.request}`.toLowerCase().includes(term)) : e.runs;

  return (
    <Screen title="Workflow runs" crumb="Last 7 days">
      <FixtureNotice owner="S4" what="Workflow run history" />

      <MetricTiles metrics={e.metrics} />

      <Toolbar>
        <input
          className="input"
          style={{ maxWidth: 260 }}
          placeholder="Search by request or student"
          aria-label="Search by request or student"
          value={search}
          onChange={(ev) => setSearch(ev.target.value)}
        />
        <Select label="Status" options={["All", "Completed", "Failed"]} />
        <Select label="Agent" options={["All", "Planner", "Scheduling", "Resource", "Validation"]} />
        <Select label="Sort" options={["Newest", "Slowest", "Failed first"]} />
      </Toolbar>

      <div className="table-scroll">
        <table className="table">
          <thead>
            <tr>
              <th>Run</th>
              <th>Request</th>
              <th>Started</th>
              <th>Duration</th>
              <th>Last step</th>
              <th>Status</th>
              <th>Result</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {runs.map((r) => (
              <tr key={r.id}>
                <td>
                  <b>{r.id}</b>
                </td>
                <td>{r.request}</td>
                <td>{r.started}</td>
                <td>{r.duration}</td>
                <td>{r.lastStep}</td>
                <td>
                  <TagOf tag={r.status} />
                </td>
                <td>{r.result}</td>
                <td>
                  <Link to={`/workflows/${r.id}`}>{r.action === "retry" ? "Retry" : "Open"}</Link>
                </td>
              </tr>
            ))}
            {runs.length === 0 && (
              <tr>
                <td colSpan={8}>
                  <div className="state-view">No run matches “{search}”.</div>
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <Pagination showing={`Showing 1–${runs.length} of ${e.total}`} disablePrevious />
    </Screen>
  );
}
