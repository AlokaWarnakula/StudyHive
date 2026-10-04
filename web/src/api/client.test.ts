import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { apiFetch, ApiError, OFFLINE_MESSAGE, REQUEST_TIMEOUT_MS, TIMEOUT_MESSAGE } from "./client";
import { SESSION_EXPIRED_MESSAGE, useAuthStore } from "../store/authStore";

/** W-01 (refresh on 401 via the httpOnly cookie) and CW-02 (field errors, timeout). */

const librarian = { id: "u1", name: "Test Librarian", email: "lib@studyhive.test", role: "Librarian" as const };

function json(status: number, body: unknown): Response {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

const refreshed = {
  accessToken: "access-2",
  accessTokenExpiresAt: "2026-01-01T00:00:00Z",
  refreshTokenExpiresAt: "2026-02-01T00:00:00Z",
  user: { id: "u1", email: "lib@studyhive.test", fullName: "Test Librarian", role: "Librarian" },
};

/** A fake API: the server only accepts `valid`; /refresh answers with `refreshStatus`. */
function fakeApi(opts: { refreshStatus?: number; refreshDelayMs?: number; revalidates?: boolean } = {}) {
  const state = { valid: "access-2", refreshCalls: 0, calls: [] as { url: string; init: RequestInit }[] };
  const fetchMock = vi.fn(async (url: string, init: RequestInit) => {
    state.calls.push({ url, init });
    if (url.includes("/api/auth/refresh")) {
      state.refreshCalls++;
      if (opts.refreshDelayMs) await new Promise((r) => setTimeout(r, opts.refreshDelayMs));
      const status = opts.refreshStatus ?? 200;
      if (status !== 200) return json(status, { title: "Refresh failed" });
      if (opts.revalidates === false) state.valid = "never";
      return json(200, refreshed);
    }
    const auth = (init.headers as Record<string, string>).Authorization;
    if (auth !== `Bearer ${state.valid}`) return json(401, { title: "Unauthorized" });
    return json(200, { id: "r1" });
  });
  vi.stubGlobal("fetch", fetchMock);
  return state;
}

describe("apiFetch session refresh (W-01)", () => {
  beforeEach(() => {
    useAuthStore.getState().logout();
    useAuthStore.setState({ signedOutReason: null });
    useAuthStore.getState().login(librarian, "access-1"); // access-1 has expired on the fake server
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
    useAuthStore.getState().logout();
  });

  it("refreshes once through the cookie and retries the call with the new token", async () => {
    const api = fakeApi();

    const result = await apiFetch<{ id: string }>("/api/rooms/r1", { token: "access-1" });

    expect(result.id).toBe("r1");
    expect(api.refreshCalls).toBe(1);
    const refresh = api.calls.find((c) => c.url.includes("/api/auth/refresh"))!;
    expect(refresh.url).toContain("?client=web");
    expect(refresh.init.credentials).toBe("include");
    expect(refresh.init.body).toBe("{}");
    expect(useAuthStore.getState().accessToken).toBe("access-2");
    const retry = api.calls.at(-1)!;
    expect((retry.init.headers as Record<string, string>).Authorization).toBe("Bearer access-2");
  });

  it("shares one refresh between calls that hit a 401 at the same time", async () => {
    const api = fakeApi({ refreshDelayMs: 30 });

    const results = await Promise.all([1, 2, 3].map(() => apiFetch("/api/rooms/r1", { token: "access-1" })));

    expect(results).toHaveLength(3);
    expect(api.refreshCalls).toBe(1);
  });

  it("does not retry a second time when the refreshed token is refused too", async () => {
    const api = fakeApi({ revalidates: false });
    api.valid = "never";

    await expect(apiFetch("/api/rooms/r1", { token: "access-1" })).rejects.toMatchObject({ status: 401 });
    expect(api.refreshCalls).toBe(1);
    expect(api.calls.filter((c) => c.url.includes("/api/rooms/r1"))).toHaveLength(2);
  });

  it("signs out with a reason when the refresh cookie is rejected", async () => {
    fakeApi({ refreshStatus: 401 });

    await expect(apiFetch("/api/rooms/r1", { token: "access-1" })).rejects.toMatchObject({ status: 401 });
    expect(useAuthStore.getState().user).toBeNull();
    expect(useAuthStore.getState().accessToken).toBeNull();
    expect(useAuthStore.getState().signedOutReason).toBe(SESSION_EXPIRED_MESSAGE);
  });

  it.each([429, 500])("keeps the session when refresh answers %i", async (status) => {
    fakeApi({ refreshStatus: status });

    await expect(apiFetch("/api/rooms/r1", { token: "access-1" })).rejects.toMatchObject({ status: 401 });
    expect(useAuthStore.getState().user).not.toBeNull();
    expect(useAuthStore.getState().signedOutReason).toBeNull();
  });

  it("stays signed out when the user signs out during a slow refresh", async () => {
    fakeApi({ refreshDelayMs: 30 });

    const call = apiFetch("/api/rooms/r1", { token: "access-1" });
    await new Promise((r) => setTimeout(r, 5));
    useAuthStore.getState().logout();

    await expect(call).rejects.toBeInstanceOf(ApiError);
    expect(useAuthStore.getState().user).toBeNull();
    expect(useAuthStore.getState().accessToken).toBeNull();
  });

  it("never refreshes for the auth endpoints themselves", async () => {
    const api = fakeApi();
    vi.stubGlobal("fetch", vi.fn(async () => json(401, { title: "Invalid credentials" })));

    await expect(apiFetch("/api/auth/login?client=web", { method: "POST", body: {}, token: "access-1" })).rejects.toMatchObject({ status: 401 });
    expect(api.refreshCalls).toBe(0);
  });
});

describe("ApiError messages (CW-02)", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it("uses the first field error when the problem has no detail", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        json(400, {
          title: "One or more validation errors occurred.",
          errors: { QrCode: ["A room with this QR code already exists."] },
        }),
      ),
    );

    await expect(apiFetch("/api/rooms", { method: "POST", body: {} })).rejects.toThrow("A room with this QR code already exists.");
  });

  it("prefers the problem detail when there is one", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => json(409, { title: "Conflict", detail: "That slot is taken." })));

    await expect(apiFetch("/api/rooms", { method: "POST", body: {} })).rejects.toThrow("That slot is taken.");
  });

  it("gives up after 15 seconds with a friendly message", async () => {
    vi.useFakeTimers();
    vi.stubGlobal(
      "fetch",
      vi.fn(
        (_url: string, init: RequestInit) =>
          new Promise<Response>((_resolve, reject) => {
            init.signal?.addEventListener("abort", () => reject(new DOMException("aborted", "AbortError")));
          }),
      ),
    );

    const call = apiFetch("/api/rooms");
    const assertion = expect(call).rejects.toThrow(TIMEOUT_MESSAGE);
    await vi.advanceTimersByTimeAsync(REQUEST_TIMEOUT_MS);
    await assertion;
  });

  it("says when the API cannot be reached", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => { throw new TypeError("Failed to fetch"); }));

    await expect(apiFetch("/api/rooms")).rejects.toThrow(OFFLINE_MESSAGE);
  });
});
