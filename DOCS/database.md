# Database setup — PostgreSQL

Local Postgres runs through Docker Compose (`docker-compose.yml` at repo root). No manual install
needed.

> All values below are **development-only defaults**, safe to keep in Git because they only work
> against a container on your own machine. Never put a real/production connection string or
> password in this file, in `appsettings.json`, or anywhere else in the repo — those go in each
> developer's local, git-ignored `.env` / `appsettings.Development.json` overrides, or in the
> hosting provider's secret store for deployed environments.

## Connection details (dev)

| Setting | Value |
|---|---|
| Host | `localhost` |
| Port | `5432` |
| Database | `studyhive` |
| Username | `studyhive` |
| Password | `studyhive_dev` |
| Container name | `studyhive-db` |
| Image | `postgres:16-alpine` |

Full connection string (matches `api/src/StudyHive.Api/appsettings.Development.json`):

```
Host=localhost;Port=5432;Database=studyhive;Username=studyhive;Password=studyhive_dev
```

## Commands

Start the database (from repo root):

```bash
docker compose up -d db
```

Check it's healthy:

```bash
docker compose ps
```

Apply EF Core migrations (from `api/`):

```bash
dotnet ef database update --project src/StudyHive.Api --startup-project src/StudyHive.Api
```

Stop the database (data persists in the `studyhive-db-data` volume):

```bash
docker compose stop db
```

Reset the database completely (drops all data — re-run migrations afterward):

```bash
docker compose down -v
docker compose up -d db
dotnet ef database update --project api/src/StudyHive.Api --startup-project api/src/StudyHive.Api
```

Open a psql shell inside the running container:

```bash
docker exec -it studyhive-db psql -U studyhive -d studyhive
```

Back up the database to a local file:

```bash
docker exec studyhive-db pg_dump -U studyhive studyhive > backup.sql
```

Restore from a backup file:

```bash
cat backup.sql | docker exec -i studyhive-db psql -U studyhive -d studyhive
```

## Production / hosted environments

The relay plan (see `DOCS/StudyHive_Master_Project_Relay_Plan.html`, sec. 13) specifies Neon
(hosted PostgreSQL) for deployed environments. When that's set up, its connection string is
supplied only via the hosting platform's environment variables / secret manager — never committed
here or anywhere in the repo.
