# web/ — StudyHive staff dashboard

React + Vite + TypeScript. State: **Zustand** (ADR-1). Talks only to `api/`.

See the [root README](../README.md) for full local setup. Quick start:

```bash
cp .env.example .env
npm install
npm run dev      # http://localhost:5173
npm run test     # Vitest + Testing Library
npm run build    # tsc -b && vite build
```

## Structure

- `src/theme/tokens.ts` — colors lifted from `UI/StudyHive Web UI (offline).html`
- `src/api/client.ts` — thin fetch wrapper, bearer auth, RFC7807 error parsing (`ApiError`)
- `src/store/authStore.ts` — session state
- `src/routes/ProtectedRoute.tsx` — redirects to `/login` when unauthenticated or wrong role
- `src/components/AppShell.tsx` — side-nav layout shared by all staff pages
- `src/pages/{auth,librarian,store,admin}/` — one placeholder page per owner (S1–S4), replaced as
  each component is built
