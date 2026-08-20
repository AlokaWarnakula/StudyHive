import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { login } from "../../api/auth";
import { ApiError } from "../../api/client";
import { useAuthStore, type StaffRole } from "../../store/authStore";

const STAFF_ROLES: StaffRole[] = ["Librarian", "StoreOfficer", "Admin"];

function isStaffRole(role: string): role is StaffRole {
  return (STAFF_ROLES as string[]).includes(role);
}

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
      navigate("/requests", { replace: true });
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong. Please try again.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="page page--centered">
      <h1>Sign in to StudyHive</h1>
      <form onSubmit={handleSubmit} noValidate>
        <label htmlFor="email">Email</label>
        <input
          id="email"
          name="email"
          type="email"
          autoComplete="email"
          required
          value={email}
          onChange={(e) => setEmail(e.target.value)}
        />

        <label htmlFor="password">Password</label>
        <input
          id="password"
          name="password"
          type="password"
          autoComplete="current-password"
          required
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />

        {error && (
          <p role="alert" className="form-error">
            {error}
          </p>
        )}

        <button type="submit" disabled={submitting}>
          {submitting ? "Signing in…" : "Sign in"}
        </button>
      </form>
    </div>
  );
}
