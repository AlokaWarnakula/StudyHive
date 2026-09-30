import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { listApprovals, type ApprovalQueueStatus } from "../../api/approvals";
import { Screen } from "../../components/AppShell";
import { Pagination, Select, Tag, Toolbar } from "../../components/ui";
import { useAuthStore } from "../../store/authStore";
import { formatDateTime, formatMoney, humanize, showingRange, statusTone, useLoad } from "./s4";

const STATUS_OPTIONS = ["Pending", "Approved", "Rejected", "Revision requested", "All"] as const;
const STATUS_VALUES: Record<(typeof STATUS_OPTIONS)[number], ApprovalQueueStatus | undefined> = {
  Pending: "Pending",
  Approved: "Approved",
  Rejected: "Rejected",
  "Revision requested": "RevisionRequested",
  All: undefined,
};

const SORT_OPTIONS = ["Oldest first", "Newest first", "Highest total"] as const;
const SORT_VALUES: Record<(typeof SORT_OPTIONS)[number], { sortBy: string; sortDir: "asc" | "desc" }> = {
  "Oldest first": { sortBy: "createdAt", sortDir: "asc" },
  "Newest first": { sortBy: "createdAt", sortDir: "desc" },
  "Highest total": { sortBy: "totalAmount", sortDir: "desc" },
};

const PAGE_SIZE = 20;

/**
 * W-03 · Approval queue — GET /api/approvals, one row per quotation. The API always lists pending
 * quotations first; the sort orders within that. Each row opens W-04, where the decision is made.
 */
export function ApprovalQueuePage() {
  const token = useAuthStore((s) => s.accessToken);
  const navigate = useNavigate();
  const [status, setStatus] = useState<(typeof STATUS_OPTIONS)[number]>("Pending");
  const [sort, setSort] = useState<(typeof SORT_OPTIONS)[number]>("Oldest first");
  const [searchDraft, setSearchDraft] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);

  const params = { status: STATUS_VALUES[status], search: search || undefined, page, pageSize: PAGE_SIZE, ...SORT_VALUES[sort] };
  const queue = useLoad(
    () => (token ? listApprovals(token, params) : null),
    JSON.stringify(params),
    "Failed to load the approval queue.",
  );
  const result = queue.data;

  return (
    <Screen
      title="Approvals"
      crumb={result ? `${result.totalItems} ${status === "All" ? "in total" : status.toLowerCase()}` : "Waiting for a decision"}
    >
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
            style={{ maxWidth: 280 }}
            placeholder="Search the student's objective"
            aria-label="Search the student's objective"
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

      {queue.error && <p role="alert" className="form-error">{queue.error}</p>}
      {queue.loading && !result && <div className="state-view">Loading…</div>}

      {result && (
        <>
          <div className="table-scroll">
            <table className="table">
              <thead>
                <tr>
                  <th>Objective</th>
                  <th>People</th>
                  <th>Room fee</th>
                  <th>Items</th>
                  <th>Total</th>
                  <th>Budget</th>
                  <th>Status</th>
                  <th>Quoted</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {result.items.map((row) => (
                  <tr key={row.quotationId}>
                    <td>
                      <b>{row.objective}</b>
                      <div className="fnote">Version {row.version}</div>
                    </td>
                    <td>{row.groupSize}</td>
                    <td>{formatMoney(row.roomFee, row.currency)}</td>
                    <td>{formatMoney(row.consumableCost, row.currency)}</td>
                    <td>
                      <b>{formatMoney(row.totalAmount, row.currency)}</b>
                    </td>
                    <td>
                      <Tag tone={row.withinBudget ? "accent" : "outline"}>
                        {row.withinBudget ? "Within budget" : "Over budget"}
                      </Tag>
                      <div className="fnote">{formatMoney(row.budgetSnapshot, row.currency)}</div>
                    </td>
                    <td>
                      <Tag tone={row.status === "Pending" ? "neutral" : statusTone(row.status)}>{humanize(row.status)}</Tag>
                    </td>
                    <td>{formatDateTime(row.createdAt)}</td>
                    <td>
                      <button
                        type="button"
                        className={row.status === "Pending" ? "btn btn-primary" : "btn btn-secondary"}
                        onClick={() => navigate(`/approvals/${row.quotationId}`)}
                      >
                        {row.status === "Pending" ? "Review" : "Open"}
                      </button>
                    </td>
                  </tr>
                ))}
                {result.items.length === 0 && (
                  <tr>
                    <td colSpan={9}>
                      <div className="state-view">
                        {search ? `No quotation matches “${search}”.` : "Nothing here — the queue is clear."}
                      </div>
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
