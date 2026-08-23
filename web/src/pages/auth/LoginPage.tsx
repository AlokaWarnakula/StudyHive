import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { login } from "../../api/auth";
import { ApiError } from "../../api/client";
import { useAuthStore, type StaffRole } from "../../store/authStore";
import { Placeholder } from "../../components/ui";

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
  const navigate = useNavigate();

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      const tokens = await login(email, password);

      if (!isStaffRole(tokens.user.role)) {
        // This dashboard is staff-only (DOCS §01/02) — students use the Flutter app.
        setError("This account can't sign in to the staff dashboard. Use the StudyHive mobile app instead.");
        return;
      }

      storeLogin(
        { id: tokens.user.id, name: tokens.user.fullName, email: tokens.user.email, role: tokens.user.role },
        tokens.accessToken,
        tokens.refreshToken,
      );
      // The dashboard is the one screen every staff role can reach, so it is a safe landing place
      // for a Librarian, a StoreOfficer and an Admin alike.
      navigate("/", { replace: true });
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

          {/* The reference offers "keep me signed in", but the staff console deliberately holds its
              tokens in memory only (see store/authStore.ts) — so the control is shown disabled with
              the reason, rather than silently doing nothing. */}
          <label className="radio">
            <input type="checkbox" disabled />
            <span className="dot" />
            <span className="text-muted">Keep me signed in — off for staff, tokens are never stored</span>
          </label>

          {error && (
            <p role="alert" className="form-error">
              {error}
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
