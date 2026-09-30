import { useState } from "react";
import { Link } from "react-router-dom";
import { listWorkflowExecutions, type WorkflowStatus } from "../../api/approvals";
import { Screen } from "../../components/AppShell";
import { Pagination, Select, Tag, Toolbar } from "../../components/ui";
import { useAuthStore } from "../../store/authStore";
import { formatDateTime, humanize, showingRange, statusTone, useLoad } from "./s4";

const STATUS_OPTIONS = ["All", "Failed", "Pending approval", "Approved", "Rejected", "In progress", "Completed"] as const;
const STATUS_VALUES: Record<(typeof STATUS_OPTIONS)[number], WorkflowStatus | undefined> = {
  All: undefined,
  Failed: "Failed",
  "Pending approval": "PendingApproval",
  Approved: "Approved",
  Rejected: "Rejected",
  "In progress": "InProgress",
  Completed: "Completed",
};

const SORT_OPTIONS = ["Newest", "Oldest", "Recently finished"] as const;
const SORT_VALUES: Record<(typeof SORT_OPTIONS)[number], { sortBy: string; sortDir: "asc" | "desc" }> = {
  Newest: { sortBy: "startedAt", sortDir: "desc" },
  Oldest: { sortBy: "startedAt", sortDir: "asc" },
  "Recently finished": { sortBy: "completedAt", sortDir: "desc" },
};

const PAGE_SIZE = 20;

function duration(startedAt: string, completedAt: string | null): string {
  if (!completedAt) return "—";
  const seconds = Math.max(0, (new Date(completedAt).getTime() - new Date(startedAt).getTime()) / 1000);
  return seconds < 60 ? `${seconds.toFixed(1)} s` : `${Math.floor(seconds / 60)} min ${Math.round(seconds % 60)} s`;
}

/**
 * W-07 · Execution history — GET /api/workflow-executions. The API lists failed runs first, because
 * finding those is why this screen exists; the error code and message say what went wrong.
 */
export function ExecutionHistoryPage() {
  const token = useAuthStore((s) => s.accessToken);
  const [status, setStatus] = useState<(typeof STATUS_OPTIONS)[number]>("All");
  const [sort, setSort] = useState<(typeof SORT_OPTIONS)[number]>("Newest");
  const [searchDraft, setSearchDraft] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);

  const params = { status: STATUS_VALUES[status], search: search || undefined, page, pageSize: PAGE_SIZE, ...SORT_VALUES[sort] };
  const runs = useLoad(
    () => (token ? listWorkflowExecutions(token, params) : null),
    JSON.stringify(params),
    "Failed to load workflow runs.",
  );
  const result = runs.data;

  return (
    <Screen title="Workflow runs" crumb={result ? `${result.totalItems} runs · failed first` : "Failed first"}>
      <Toolbar>
        <form
          style={{ display: "flex", gap: 8 }}
          onSubmit={(e) => {
            e.preventDefault();
            setSearch(searchDraft.trim());
            setPage(1);
          }}
        >
          <input
            className="input"
            style={{ maxWidth: 260 }}
            placeholder="Search the objective"
            aria-label="Search the objective"
            value={searchDraft}
            onChange={(e) => setSearchDraft(e.target.value)}
          />
          <button type="submit" className="btn btn-secondary">Search</button>
        </form>
        <Select
          label="Status"
          options={STATUS_OPTIONS}
          value={status}
          onChange={(v) => {
            setStatus(v as (typeof STATUS_OPTIONS)[number]);
            setPage(1);
          }}
        />
        <Select label="Sort" options={SORT_OPTIONS} value={sort} onChange={(v) => setSort(v as (typeof SORT_OPTIONS)[number])} />
      </Toolbar>

      {runs.error && <p role="alert" className="form-error">{runs.error}</p>}
      {runs.loading && !result && <div className="state-view">Loading…</div>}

      {result && (
        <>
          <div className="table-scroll">
            <table className="table">
              <thead>
                <tr>
                  <th>Objective</th>
                  <th>Started</th>
                  <th>Duration</th>
                  <th>Step</th>
                  <th>Status</th>
                  <th>Result</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {result.items.map((run) => (
                  <tr key={run.id}>
                    <td>
                      <b>{run.objective}</b>
                      <div className="fnote">{run.id.slice(0, 8)}</div>
                    </td>
                    <td>{formatDateTime(run.startedAt)}</td>
                    <td>{duration(run.startedAt, run.completedAt)}</td>
                    <td>{`${run.currentStep} / ${run.totalSteps ?? "?"}`}</td>
                    <td>
                      <Tag tone={statusTone(run.status)}>{humanize(run.status)}</Tag>
                    </td>
                    <td>
                      {run.errorCode ? (
                        <>
                          <b>{run.errorCode}</b>
                          {run.errorMessage && <div className="fnote">{run.errorMessage}</div>}
                        </>
                      ) : (
                        "—"
                      )}
                    </td>
                    <td>
                      <Link to={`/workflows/${run.id}`}>Open</Link>
                    </td>
                  </tr>
                ))}
                {result.items.length === 0 && (
                  <tr>
                    <td colSpan={7}>
                      <div className="state-view">{search ? `No run matches “${search}”.` : "No workflow runs yet."}</div>
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          <Pagination
            showing={showingRange(result)}
            onPrevious={() => setPage((p) => p - 1)}
            onNext={() => setPage((p) => p + 1)}
            disablePrevious={result.page <= 1}
            disableNext={result.page >= result.totalPages}
          />
        </>
      )}
    </Screen>
  );
}
