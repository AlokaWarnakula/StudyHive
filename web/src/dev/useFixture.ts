import { fixtures, type Fixtures } from "./fixtures";

/**
 * The gate in front of `fixtures.ts`.
 *
 * Both conditions below are compile-time constants, so in a production build the first line of
 * `useFixture` becomes `if (true) return { enabled: false, data: null }`: `fixtures` is then
 * referenced only from unreachable code and Rollup drops the whole module. The seeded S2-S4
 * content is physically absent from a production bundle, so those screens show the honest
 * "not built yet" state rather than demo data.
 *
 * `MODE` is checked as well as `DEV` on purpose. `import.meta.env.DEV` follows `NODE_ENV`, so a
 * `vite build` run with `NODE_ENV=test` (which is what a test runner sets) leaves `DEV` true and
 * would ship the fixture data. `MODE` comes from the build command itself and is "production" for
 * any `vite build`, so the pair is safe under both.
 *
 * That claim is checked, not assumed — see `web/tests/fixtures.prod.test.ts`.
 *
 * `VITE_DISABLE_FIXTURES=1` turns fixtures off inside a development build too, which is how you
 * check that the unavailable states still read well.
 */

export const FIXTURES_ENABLED: boolean =
  import.meta.env.MODE !== "production" &&
  import.meta.env.DEV &&
  import.meta.env.VITE_DISABLE_FIXTURES !== "1";

export type FixtureResult<T> = { enabled: true; data: T } | { enabled: false; data: null };

/**
 * Reads one branch of the reference fixture set.
 *
 * @param select picks the slice this screen needs, e.g. `(f) => f.rooms`
 */
export function useFixture<T>(select: (f: Fixtures) => T): FixtureResult<T> {
  // Written as a literal condition rather than `!FIXTURES_ENABLED` so the bundler can fold it.
  if (import.meta.env.MODE === "production" || !import.meta.env.DEV) {
    return { enabled: false, data: null };
  }
  if (!FIXTURES_ENABLED) {
    return { enabled: false, data: null };
  }
  return { enabled: true, data: select(fixtures) };
}
