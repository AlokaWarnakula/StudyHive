# infra/

Deployment and infrastructure assets that don't belong in an application source tree, beyond the
local-dev `docker-compose.yml` at the repo root. See [`DOCS/StudyHive_Master_Project_Relay_Plan.html`](../DOCS/StudyHive_Master_Project_Relay_Plan.html)
§15 (Deployment & demo) and §13 (locked decisions) for the target hosts.

## Locked deployment targets (ADR-5)

| Component | Host | Notes |
|---|---|---|
| PostgreSQL | Neon | Real free tier, database branching |
| `api/` + `agent/` | Render | One platform, one env-var story |
| `web/` | Vercel | Zero-config Vite deploys, PR preview URLs |
| `mobile/` | — | Ships as an APK, not hosted |

## What goes here

- Render service definitions / `render.yaml` once deployment is configured.
- Any Neon connection/branching notes that aren't secrets (the actual connection string is an
  environment variable on Render, never committed).
- Vercel project config if it needs anything beyond `web/vercel.json` defaults.

## What does *not* go here

- Local development infrastructure — that's `docker-compose.yml` at the repo root.
- Secrets of any kind (API keys, connection strings, signing keys). Every environment outside
  Development must supply its own via the hosting platform's environment variables — see the
  startup checks in `api/src/StudyHive.Api/Program.cs` (`ConnectionStrings:Default`,
  `Jwt:SigningKey`, `AllowedHosts`) and `agent/app/settings.py` (`INTERNAL_API_KEY`).

Empty until deployment is actually configured (post Foundation Lock) — this file exists so the
directory the root `README.md` promises isn't a broken reference.
