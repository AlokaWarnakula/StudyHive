import { useAuthStore, type AuthUser, type StaffRole } from "../store/authStore";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5299";

/** CW-02 / C-12: every request gives up after this long, with a message a person can act on. */
export const REQUEST_TIMEOUT_MS = 15_000;
export const TIMEOUT_MESSAGE = "StudyHive is taking too long to answer. Check your connection and try again.";
export const OFFLINE_MESSAGE = "Can't reach StudyHive. Check your connection and try again.";

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

/** The first field message of an ASP.NET validation problem, e.g. "A room with this QR code already exists." */
function firstFieldError(problem: ProblemDetails): string | undefined {
  for (const messages of Object.values(problem.errors ?? {})) {
    if (Array.isArray(messages) && messages.length > 0) return messages[0];
  }
  return undefined;
}

export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails;

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail ?? firstFieldError(problem) ?? problem.title ?? `Request failed with status ${status}`);
    this.status = status;
    this.problem = problem;
  }
}

interface RequestOptions extends Omit<RequestInit, "body"> {
  body?: unknown;
  token?: string | null;
}

async function send(path: string, options: RequestOptions, token: string | null | undefined): Promise<Response> {
  const { body, token: _ignored, headers, signal, ...rest } = options;
  void _ignored;

  const controller = new AbortController();
  let timedOut = false;
  const timer = setTimeout(() => {
    timedOut = true;
    controller.abort();
  }, REQUEST_TIMEOUT_MS);
  const onCallerAbort = () => controller.abort();
  signal?.addEventListener("abort", onCallerAbort);

  try {
    return await fetch(`${API_BASE_URL}${path}`, {
      ...rest,
      // Only the auth endpoints read the refresh cookie (it is scoped to /api/auth).
      ...(path.startsWith("/api/auth/") ? { credentials: "include" as const } : {}),
      signal: controller.signal,
      headers: {
        "Content-Type": "application/json",
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...headers,
      },
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  } catch (err) {
    if (timedOut) throw new ApiError(0, { title: "Timed out", detail: TIMEOUT_MESSAGE });
    if (signal?.aborted) throw err; // the caller cancelled; let them see their own AbortError
    throw new ApiError(0, { title: "Offline", detail: OFFLINE_MESSAGE });
  } finally {
    clearTimeout(timer);
    signal?.removeEventListener("abort", onCallerAbort);
  }
}

async function problemOf(response: Response): Promise<ApiError> {
  const problem: ProblemDetails = await response
    .json()
    .catch(() => ({ title: response.statusText, status: response.status }));
  return new ApiError(response.status, problem);
}

/**
 * Thin fetch wrapper: JSON in/out, bearer auth, RFC7807 errors surfaced as ApiError.
 *
 * W-01: a 401 on any call outside /api/auth/ refreshes the session once (shared by every call that
 * hits a 401 at the same time) and retries that call once with the new access token. A call sent
 * with an older token than the current one is simply retried.
 */
export async function apiFetch<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const sentToken = options.token;
  let response = await send(path, options, sentToken);

  if (response.status === 401 && sentToken && !path.startsWith("/api/auth/")) {
    const current = useAuthStore.getState().accessToken;
    const canRetry = current !== null && current !== sentToken ? true : await refreshSession();
    const retryToken = useAuthStore.getState().accessToken;
    if (canRetry && retryToken) {
      response = await send(path, options, retryToken);
    }
  }

  if (!response.ok) {
    throw await problemOf(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

interface RefreshResponse {
  accessToken: string;
  user: { id: string; email: string; fullName: string; role: string };
}

const STAFF_ROLES: StaffRole[] = ["Librarian", "StoreOfficer", "Admin"];

export function toStaffUser(user: RefreshResponse["user"]): AuthUser | null {
  return (STAFF_ROLES as string[]).includes(user.role)
    ? { id: user.id, name: user.fullName, email: user.email, role: user.role as StaffRole }
    : null;
}

let refreshing: Promise<boolean> | null = null;

/**
 * Exchanges the httpOnly refresh cookie for a new access token. Used after a 401 and once on app start
 * (SessionRestore), so a reload keeps staff signed in. Only a rejected cookie (400/401) ends the
 * session; offline, a timeout, a 429 from the auth rate limiter or a 5xx keep it. A refresh that
 * lands after a sign-out (or a new sign-in) is thrown away.
 */
export function refreshSession(): Promise<boolean> {
  refreshing ??= doRefresh().finally(() => {
    refreshing = null;
  });
  return refreshing;
}

async function doRefresh(): Promise<boolean> {
  const epoch = useAuthStore.getState().epoch;
  let response: Response;
  try {
    response = await send("/api/auth/refresh?client=web", { method: "POST", body: {} }, null);
  } catch {
    return false;
  }
  if (useAuthStore.getState().epoch !== epoch) return false;

  if (!response.ok) {
    if (response.status === 400 || response.status === 401) useAuthStore.getState().expire();
    return false;
  }

  const tokens = (await response.json()) as RefreshResponse;
  const user = toStaffUser(tokens.user);
  if (!user) {
    // A non-staff cookie (e.g. a student who tried this page) is revoked, not rotated forever.
    await send("/api/auth/logout?client=web", { method: "POST", body: {} }, null).catch(() => undefined);
    useAuthStore.getState().expire();
    return false;
  }
  useAuthStore.getState().login(user, tokens.accessToken);
  return true;
}
