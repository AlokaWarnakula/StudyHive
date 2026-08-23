import { execFileSync } from "node:child_process";
import { mkdtempSync, readFileSync, readdirSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterAll, describe, expect, it } from "vitest";

/**
 * The claim in `useFixture.ts` is that the seeded S2-S4 reference data is physically absent from a
 * production bundle, not merely hidden behind a runtime flag. That is only true while the gate
 * stays statically analysable, so this test builds for production and looks for the data.
 *
 * It runs a real Vite build, which is why it is the slowest test in the suite; without it, a small
 * refactor of `useFixture` could silently start shipping demo names to production.
 */

const out = mkdtempSync(join(tmpdir(), "studyhive-prod-"));

afterAll(() => {
  rmSync(out, { recursive: true, force: true });
});

describe("production bundle", () => {
  it("contains none of the development fixture data", () => {
    execFileSync("npx", ["vite", "build", "--outDir", out, "--emptyOutDir", "--logLevel", "error"], {
      cwd: process.cwd(),
      stdio: "pipe",
      shell: process.platform === "win32",
    });

    const assets = join(out, "assets");
    const bundle = readdirSync(assets)
      .filter((f) => f.endsWith(".js"))
      .map((f) => readFileSync(join(assets, f), "utf8"))
      .join("\n");

    expect(bundle.length).toBeGreaterThan(1000);

    // One distinctive string from each fixture branch.
    const fixtureOnlyStrings = [
      "Thesis writing", // dashboard queue + approvals
      "Rathnayake", // student names
      "resource-agent", // consumable ledger
      "WF-2291", // workflow ids
      "Lanka Stationers", // suppliers
      "PO-2211", // stock-in form
      "B-204", // room codes
      "no-reply@studyhive.lk", // settings
    ];

    // Assert on the list of offenders, not on the bundle itself — a failed `toContain` against a
    // 300KB string would print the whole bundle.
    const leaked = fixtureOnlyStrings.filter((needle) => bundle.includes(needle));
    expect(leaked, "development fixture data must not ship to production").toEqual([]);
  }, 120_000);
});
