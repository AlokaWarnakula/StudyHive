import { useEffect, useState, type ReactNode } from "react";
import { refreshSession } from "../api/client";
import { useAuthStore } from "../store/authStore";

/**
 * W-01: on a full page load the in-memory access token is gone, but the httpOnly refresh cookie is
 * not. Before any route renders, try one refresh so a reload keeps staff signed in (and a deep link
 * opens the page asked for instead of bouncing through /login). With no cookie the refresh is a
 * quiet 401 and the routes render signed out, exactly as before.
 */
export function SessionRestore({ children }: { children: ReactNode }) {
  const [ready, setReady] = useState(() => useAuthStore.getState().user !== null);

  useEffect(() => {
    if (ready) return;
    let active = true;
    refreshSession().finally(() => {
      if (active) setReady(true);
    });
    return () => {
      active = false;
    };
  }, [ready]);

  if (!ready) {
    return (
      <p role="status" className="text-muted" style={{ padding: 24 }}>
        Loading StudyHive…
      </p>
    );
  }
  return <>{children}</>;
}
