import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { App } from "../App";
import { SessionRestore } from "./SessionRestore";
import { safeNextPath } from "../routes/safeNextPath";
import { SESSION_EXPIRED_MESSAGE, useAuthStore } from "../store/authStore";

/** W-01: a reload keeps the session; CW-09: sign-in returns to the page that asked for it. */

function json(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

const emptyPage = { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 };

function tokens(role: string) {
  return {
    accessToken: "access-restored",
    accessTokenExpiresAt: "2026-01-01T00:00:00Z",
    refreshTokenExpiresAt: "2026-02-01T00:00:00Z",
    user: { id: "u1", email: "lib@studyhive.test", fullName: "Test Librarian", role, isActive: true, createdAt: "2026-01-01T00:00:00Z" },
  };
}

function renderApp(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <SessionRestore>
        <App />
      </SessionRestore>
    </MemoryRouter>,
  );
}

describe("SessionRestore and deep links", () => {
  beforeEach(() => {
    useAuthStore.getState().logout();
    useAuthStore.setState({ signedOutReason: null });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    useAuthStore.getState().logout();
  });

  it("restores the session from the refresh cookie on a reload and opens the page asked for", async () => {
    const fetchMock = vi.fn(async (url: string) =>
      url.includes("/api/auth/refresh") ? json(200, tokens("Librarian")) : json(200, emptyPage),
    );
    vi.stubGlobal("fetch", fetchMock);

    renderApp("/requests");

    await waitFor(() => expect(screen.getByRole("heading", { name: "Booking requests" })).toBeInTheDocument());
    expect(useAuthStore.getState().accessToken).toBe("access-restored");
    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toContain("/api/auth/refresh?client=web");
    expect(init.credentials).toBe("include");
  });

  it("shows sign-in quietly when there is no session to restore, keeping the deep link", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => json(401, { title: "Invalid refresh token" })));

    renderApp("/requests");

    await waitFor(() => expect(screen.getByRole("heading", { name: "Sign in to StudyHive" })).toBeInTheDocument());
    expect(screen.queryByRole("alert")).toBeNull(); // a first visit is not an "expired" session
  });

  it("returns to the deep link after signing in", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async (url: string) => {
        if (url.includes("/api/auth/refresh")) return json(401, { title: "Invalid refresh token" });
        if (url.includes("/api/auth/login")) return json(200, tokens("Librarian"));
        return json(200, emptyPage);
      }),
    );

    renderApp("/requests");
    await waitFor(() => expect(screen.getByLabelText("Work email")).toBeInTheDocument());
    fireEvent.change(screen.getByLabelText("Work email"), { target: { value: "lib@studyhive.test" } });
    fireEvent.change(screen.getByLabelText("Password"), { target: { value: "correct-password" } });
    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));

    await waitFor(() => expect(screen.getByRole("heading", { name: "Booking requests" })).toBeInTheDocument());
  });

  it("tells the user why when the session expired", async () => {
    useAuthStore.setState({ signedOutReason: SESSION_EXPIRED_MESSAGE });
    vi.stubGlobal("fetch", vi.fn(async () => json(401, { title: "Invalid refresh token" })));

    renderApp("/login");

    await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent(SESSION_EXPIRED_MESSAGE));
  });

  it("only accepts in-app paths as a sign-in destination", () => {
    expect(safeNextPath("/rooms?tab=2")).toBe("/rooms?tab=2");
    expect(safeNextPath(null)).toBe("/");
    expect(safeNextPath("https://evil.example")).toBe("/");
    expect(safeNextPath("//evil.example")).toBe("/");
    expect(safeNextPath("/\\evil.example")).toBe("/");
    expect(safeNextPath("rooms")).toBe("/");
  });
});
