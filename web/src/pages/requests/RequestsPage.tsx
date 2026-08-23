import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Screen } from "../../components/AppShell";
import { Pagination, Tag, Toolbar } from "../../components/ui";
import { ApiError } from "../../api/client";
import {
  listBookingRequests,
  type BookingRequest,
  type BookingRequestStatus,
  type PagedResult,
} from "../../api/bookingRequests";
import { useAuthStore } from "../../store/authStore";
import { statusLabel, statusTone } from "./status";

const STATUS_OPTIONS: BookingRequestStatus[] = [
  "Draft",
  "Submitted",
  "Processing",
  "PendingApproval",
  "Approved",
  "Rejected",
  "RevisionRequested",
  "Completed",
  "Cancelled",
  "Failed",
];

type SortBy = "createdAt" | "status" | "budget";

const PAGE_SIZE = 20;

/**
 * W-10 · Booking requests — GET /api/booking-requests with status filter, sort, pagination and a
 * real empty state. This is a genuine S1 screen: every row below comes from the API.
 */
export function RequestsPage() {
  const token = useAuthStore((s) => s.accessToken);
  const navigate = useNavigate();

  const [result, setResult] = useState<PagedResult<BookingRequest> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState("");
  const [status, setStatus] = useState<string>("");
  const [sortBy, setSortBy] = useState<SortBy>("createdAt");
  const [sortDir, setSortDir] = useState<"asc" | "desc">("desc");
  const [page, setPage] = useState(1);

  useEffect(() => {
    if (!token) return;
    let cancelled = false;
    setLoading(true);
    setError(null);

    listBookingRequests(token, {
      page,
      pageSize: PAGE_SIZE,
      search: search || undefined,
      status: status || undefined,
      sortBy,
      sortDir,
    })
      .then((data) => {
        if (!cancelled) setResult(data);
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof ApiError ? err.message : "Failed to load booking requests.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [token, page, search, status, sortBy, sortDir]);

  function toggleSort(column: SortBy) {
    if (sortBy === column) {
      setSortDir((d) => (d === "asc" ? "desc" : "asc"));
    } else {
      setSortBy(column);
      setSortDir("desc");
    }
    setPage(1);
  }

  function sortMark(column: SortBy) {
    return sortBy === column ? (sortDir === "asc" ? "▲" : "▼") : "";
  }

  const items = result?.items ?? [];
  const firstRow = result && result.totalItems > 0 ? (result.page - 1) * result.pageSize + 1 : 0;

  return (
    <Screen title="Booking requests" crumb={result ? `${result.totalItems} in total` : undefined}>
      <Toolbar>
        <input
          className="input"
          style={{ maxWidth: 280 }}
          type="search"
          placeholder="Search student, room or purpose"
          aria-label="Search student, room or purpose"
          value={search}
          onChange={(e) => {
            setPage(1);
            setSearch(e.target.value);
          }}
        />
        <select
          className="input"
          style={{ maxWidth: 180 }}
          aria-label="Status"
          value={status}
          onChange={(e) => {
            setPage(1);
            setStatus(e.target.value);
          }}
        >
          <option value="">Status: All</option>
          {STATUS_OPTIONS.map((s) => (
            <option key={s} value={s}>
              {`Status: ${statusLabel(s)}`}
            </option>
          ))}
        </select>
      </Toolbar>

      {error && (
        <p role="alert" className="form-error">
          {error}
        </p>
      )}

      {loading && !result && <div className="state-view">Loading…</div>}

      {result && items.length === 0 && !loading && (
        <div className="state-view">No booking request matches these filters.</div>
      )}

      {items.length > 0 && (
        <>
          <div className="table-scroll">
            <table className="table">
              <thead>
                <tr>
                  <th>Request</th>
                  <th>Purpose</th>
                  <th>Requested slot</th>
                  <th>People</th>
                  <th>
                    <button type="button" onClick={() => toggleSort("budget")}>
                      Budget {sortMark("budget")}
                    </button>
                  </th>
                  <th>
                    <button type="button" onClick={() => toggleSort("status")}>
                      Status {sortMark("status")}
                    </button>
                  </th>
                  <th>
                    <button type="button" onClick={() => toggleSort("createdAt")}>
                      Updated {sortMark("createdAt")}
                    </button>
                  </th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {items.map((request) => (
                  <tr key={request.id} className="row-click" onClick={() => navigate(`/requests/${request.id}`)}>
                    <td>
                      <b>{request.id.slice(0, 8)}</b>
                    </td>
                    <td>{request.objective}</td>
                    <td>
                      {request.preferredDateFrom}
                      <div className="fnote">
                        {request.preferredTimeFrom} – {request.preferredTimeTo}
                      </div>
                    </td>
                    <td>{request.groupSize}</td>
                    <td>Rs. {request.budget.toFixed(2)}</td>
                    <td>
                      <Tag tone={statusTone(request.status)}>{statusLabel(request.status)}</Tag>
                    </td>
                    <td>{new Date(request.updatedAt).toLocaleString()}</td>
                    <td>Open</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <Pagination
            showing={`Showing ${firstRow}–${firstRow + items.length - 1} of ${result!.totalItems} · ${PAGE_SIZE} per page`}
            disablePrevious={result!.page <= 1}
            disableNext={result!.page >= result!.totalPages}
            onPrevious={() => setPage((p) => p - 1)}
            onNext={() => setPage((p) => p + 1)}
          />
        </>
      )}
    </Screen>
  );
}
