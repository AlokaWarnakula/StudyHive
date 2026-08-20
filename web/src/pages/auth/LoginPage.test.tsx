import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { App } from "../../App";
import { useAuthStore } from "../../store/authStore";

function mockFetchOnce(status: number, body: unknown) {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockResolvedValue({
      ok: status >= 200 && status < 300,
      status,
      json: async () => body,
    }),
  );
}

describe("LoginPage", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    useAuthStore.getState().logout();
  });

  beforeEach(() => {
    useAuthStore.getState().logout();
  });

  it("signs a staff user in and lands on the dashboard", async () => {
    mockFetchOnce(200, {
      accessToken: "access-token",
      accessTokenExpiresAt: "2026-01-01T00:00:00Z",
      refreshToken: "refresh-token",
      refreshTokenExpiresAt: "2026-02-01T00:00:00Z",
      user: {
        id: "11111111-1111-1111-1111-111111111111",
        email: "librarian@studyhive.test",
        fullName: "Test Librarian",
        role: "Librarian",
        isActive: true,
        createdAt: "2026-01-01T00:00:00Z",
      },
    });

    render(
      <MemoryRouter initialEntries={["/login"]}>
        <App />
      </MemoryRouter>,
    );

    fireEvent.change(screen.getByLabelText("Email"), { target: { value: "librarian@studyhive.test" } });
    fireEvent.change(screen.getByLabelText("Password"), { target: { value: "correct-password" } });
    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));

    await waitFor(() => expect(screen.getByRole("heading", { name: "Booking Requests" })).toBeInTheDocument());
    expect(useAuthStore.getState().user?.role).toBe("Librarian");
  });

  it("rejects a student account with a clear message instead of signing in", async () => {
    mockFetchOnce(200, {
      accessToken: "access-token",
      accessTokenExpiresAt: "2026-01-01T00:00:00Z",
      refreshToken: "refresh-token",
      refreshTokenExpiresAt: "2026-02-01T00:00:00Z",
      user: {
        id: "22222222-2222-2222-2222-222222222222",
        email: "student@studyhive.test",
        fullName: "Test Student",
        role: "Student",
        isActive: true,
        createdAt: "2026-01-01T00:00:00Z",
      },
    });

    render(
      <MemoryRouter initialEntries={["/login"]}>
        <App />
      </MemoryRouter>,
    );

    fireEvent.change(screen.getByLabelText("Email"), { target: { value: "student@studyhive.test" } });
    fireEvent.change(screen.getByLabelText("Password"), { target: { value: "correct-password" } });
    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));

    await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent(/mobile app/i));
    expect(useAuthStore.getState().user).toBeNull();
  });

  it("shows an error and does not navigate on invalid credentials", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 401,
        json: async () => ({ title: "Invalid credentials", status: 401, detail: "The email or password is incorrect." }),
      }),
    );

    render(
      <MemoryRouter initialEntries={["/login"]}>
        <App />
      </MemoryRouter>,
    );

    fireEvent.change(screen.getByLabelText("Email"), { target: { value: "librarian@studyhive.test" } });
    fireEvent.change(screen.getByLabelText("Password"), { target: { value: "wrong-password" } });
    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));

    await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent("The email or password is incorrect."));
    expect(screen.getByRole("heading", { name: "Sign in to StudyHive" })).toBeInTheDocument();
  });
});
