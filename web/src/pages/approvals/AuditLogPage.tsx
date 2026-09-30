import { useState } from "react";
import { listAuditLogs, type AuditLogFilters } from "../../api/approvals";
import { Screen } from "../../components/AppShell";
import { Pagination, Select, Tag, Toolbar } from "../../components/ui";
import { useAuthStore } from "../../store/authStore";
import { formatDateTime, formatPayload, showingRange, useLoad } from "./s4";

const PAGE_SIZE = 25;

const SORT_OPTIONS = ["Newest", "Oldest", "Action", "Entity"] as const;
const SORT_VALUES: Record<(typeof SORT_OPTIONS)[number], { sortBy: string; sortDir: "asc" | "desc" }> = {
  Newest: { sortBy: "createdAt", sortDir: "desc" },
  Oldest: { sortBy: "createdAt", sortDir: "asc" },
  Action: { sortBy: "action", sortDir: "asc" },
  Entity: { sortBy: "entityType", sortDir: "asc" },
};

interface Draft {
  action: string;
  entityType: string;
  entityId: string;
  userId: string;
  from: string;
  to: string;
}

const EMPTY: Draft = { action: "", entityType: "", entityId: "", userId: "", from: "", to: "" };

/** A yyyy-MM-dd date input as the start of that local day, in ISO. `to` covers its whole day. */
function dayBound(date: string, endOfDay: boolean): string | undefined {
  if (!date) return undefined;
  const [y, m, d] = date.split("-").map(Number);
  return new Date(y, m - 1, endOfDay ? d + 1 : d).toISOString();
}

/**
 * W-08 · Audit log — GET /api/audit-logs (Admin). Append-only and read-only: filter by action,
 * entity, user and date. Each row's details are the JSON the writer recorded, shown as text.
 */
export function AuditLogPage() {
  const token = useAuthStore((s) => s.accessToken);
  const [draft, setDraft] = useState<Draft>(EMPTY);
  const [applied, setApplied] = useState<Draft>(EMPTY);
  const [sort, setSort] = useState<(typeof SORT_OPTIONS)[number]>("Newest");
  const [page, setPage] = useState(1);

  const params: AuditLogFilters = {
    action: applied.action.trim() || undefined,
    entityType: applied.entityType.trim() || undefined,
    entityId: applied.entityId.trim() || undefined,
    userId: applied.userId.trim() || undefined,
    from: dayBound(applied.from, false),
    to: dayBound(applied.to, true),
    page,
    pageSize: PAGE_SIZE,
    ...SORT_VALUES[sort],
  };
  const logs = useLoad(() => (token ? listAuditLogs(token, params) : null), JSON.stringify(params), "Failed to load the audit log.");
  const result = logs.data;

  function field(key: keyof Draft, label: string, type = "text", width = 170) {
    return (
      <input
        className="input"
        style={{ maxWidth: width }}
        type={type}
        placeholder={label}
        aria-label={label}
        value={draft[key]}
        onChange={(e) => setDraft((d) => ({ ...d, [key]: e.target.value }))}
      />
    );
  }

  return (
    <Screen title="Audit log" crumb="Read-only record of every change" showUser={false}>
      <Toolbar>
        <form
          style={{ display: "flex", gap: 8, flexWrap: "wrap", alignItems: "center" }}
          onSubmit={(e) => {
            e.preventDefault();
            setApplied(draft);
            setPage(1);
          }}
        >
          {field("action", "Action, e.g. QuotationApproved", "text", 230)}
          {field("entityType", "Entity type, e.g. Quotation")}
          {field("entityId", "Entity id")}
          {field("userId", "User id")}
          {field("from", "From date", "date", 150)}
          {field("to", "To date", "date", 150)}
          <button type="submit" className="btn btn-primary">Apply</button>
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => {
              setDraft(EMPTY);
              setApplied(EMPTY);
              setPage(1);
            }}
          >
            Clear
          </button>
        </form>
        <Select label="Sort" options={SORT_OPTIONS} value={sort} onChange={(v) => setSort(v as (typeof SORT_OPTIONS)[number])} />
      </Toolbar>

      {logs.error && <p role="alert" className="form-error">{logs.error}</p>}
      {logs.loading && !result && <div className="state-view">Loading…</div>}

      {result && (
        <>
          <div className="table-scroll">
            <table className="table">
              <thead>
                <tr>
                  <th>When</th>
                  <th>User</th>
                  <th>Action</th>
                  <th>Entity</th>
                  <th>Details</th>
                  <th>IP</th>
                </tr>
              </thead>
              <tbody>
                {result.items.map((row) => (
                  <tr key={row.id}>
                    <td>{formatDateTime(row.createdAt)}</td>
                    <td>{row.userEmail ?? (row.userId ? row.userId.slice(0, 8) : "Deleted account")}</td>
                    <td>
                      <Tag tone="outline">{row.action}</Tag>
                    </td>
                    <td>
                      {row.entityType}
                      <div className="fnote">{row.entityId}</div>
                    </td>
                    <td>
                      {row.details == null ? (
                        "—"
                      ) : (
                        <details>
                          <summary>Show</summary>
                          <pre style={{ margin: 0, fontSize: 12, whiteSpace: "pre-wrap", wordBreak: "break-word" }}>
                            {formatPayload(row.details)}
                          </pre>
                        </details>
                      )}
                    </td>
                    <td>{row.ipAddress ?? "—"}</td>
                  </tr>
                ))}
                {result.items.length === 0 && (
                  <tr>
                    <td colSpan={6}>
                      <div className="state-view">No audit entry matches these filters.</div>
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
