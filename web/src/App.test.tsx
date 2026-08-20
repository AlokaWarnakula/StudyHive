import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { App } from "./App";

describe("App", () => {
  it("redirects unauthenticated users to /login", () => {
    render(
      <MemoryRouter initialEntries={["/requests"]}>
        <App />
      </MemoryRouter>,
    );

    expect(screen.getByText("Sign in to StudyHive")).toBeInTheDocument();
  });

  it("renders the login page directly", () => {
    render(
      <MemoryRouter initialEntries={["/login"]}>
        <App />
      </MemoryRouter>,
    );

    expect(screen.getByRole("heading", { name: "Sign in to StudyHive" })).toBeInTheDocument();
  });
});
