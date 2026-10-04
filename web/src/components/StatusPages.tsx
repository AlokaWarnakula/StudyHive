import { Link } from "react-router-dom";
import { Screen } from "./AppShell";

/**
 * AUDIT CW-09: a signed-in user who opens a page their role may not see, or a URL that does not
 * exist, stays signed in and inside the shell, with a way back, instead of being sent to sign-in.
 */
export function NotAllowedPage() {
  return (
    <Screen title="You don't have access">
      <div className="state-view" role="alert">
        <p style={{ marginTop: 0 }}>Your role can't open this page.</p>
        <Link to="/" className="btn btn-secondary">Go to Dashboard</Link>
      </div>
    </Screen>
  );
}

export function NotFoundPage() {
  return (
    <Screen title="Page not found">
      <div className="state-view" role="alert">
        <p style={{ marginTop: 0 }}>There is no page at this address.</p>
        <Link to="/" className="btn btn-secondary">Go to Dashboard</Link>
      </div>
    </Screen>
  );
}
