import { useState, type FormEvent } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { login, logout } from "../../api/auth";
import { ApiError } from "../../api/client";
import { useAuthStore, type StaffRole } from "../../store/authStore";
import { Placeholder } from "../../components/ui";
import { safeNextPath } from "../../routes/safeNextPath";

const STAFF_ROLES: StaffRole[] = ["Librarian", "StoreOfficer", "Admin"];

function isStaffRole(role: string): role is StaffRole {
  return (STAFF_ROLES as string[]).includes(role);
}

/**
 * W-01 · Staff sign in — POST /api/auth/login, role read from the token, no role picker.
 *
 * This is a real S1 screen: the call, the error handling and the staff-only check are unchanged;
 * only the layout now follows the reference (photograph beside a 360px form).
 */
export function LoginPage() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const storeLogin = useAuthStore((s) => s.login);
  // W-01: why the dashboard sent the user back here (the session could not be refreshed).
  const signedOutReason = useAuthStore((s) => s.signedOutReason);
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      const tokens = await login(email, password);

      if (!isStaffRole(tokens.user.role)) {
        // This dashboard is staff-only (DOCS §01/02) — students use the Flutter app. The API has
        // already set the refresh cookie, so revoke it rather than leave a live session behind.
        await logout().catch(() => undefined);
        setError("This account can't sign in to the staff dashboard. Use the StudyHive mobile app instead.");
        return;
      }

      storeLogin(
        { id: tokens.user.id, name: tokens.user.fullName, email: tokens.user.email, role: tokens.user.role },
        tokens.accessToken,
      );
      // Back to the page that asked for sign-in (CW-09), else the dashboard: the one screen every
      // staff role can reach. Only in-app paths are accepted, so ?next= can't send anyone off-site.
      navigate(safeNextPath(searchParams.get("next")), { replace: true });
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong. Please try again.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="signin">
      <div className="signin__photo">
        <Placeholder label="library photograph" height="100%" />
      </div>
      <div className="signin__panel">
        <form className="signin__form" onSubmit={handleSubmit} noValidate>
          <div className="lbl">StudyHive · staff console</div>
          <h2 style={{ margin: "0 0 6px" }}>Sign in to StudyHive</h2>

          <div className="field">
            <label htmlFor="email">Work email</label>
            <input
              id="email"
              className="input"
              name="email"
              type="email"
              autoComplete="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </div>

          <div className="field">
            <label htmlFor="password">Password</label>
            <input
              id="password"
              className="input"
              name="password"
              type="password"
              autoComplete="current-password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </div>

          {(error ?? signedOutReason) && (
            <p role="alert" className="form-error">
              {error ?? signedOutReason}
            </p>
          )}

          <button type="submit" className="btn btn-primary btn-block" style={{ padding: 12 }} disabled={submitting}>
            {submitting ? "Signing in…" : "Sign in"}
          </button>

          <p className="fnote" style={{ margin: 0 }}>
            Librarian, store officer and admin accounts all use this page. Students use the phone app.
          </p>
        </form>
      </div>
    </div>
  );
}
