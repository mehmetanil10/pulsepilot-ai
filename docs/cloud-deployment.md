# Cloud deployment and live demo

Sprint 4 Task 45 defines a Render Blueprint for the public portfolio demo. The
repository remains the source of truth: Render deploys only commits whose
GitHub checks pass and builds the same production Dockerfiles exercised by CI.

## Topology

```text
Internet
   |
   | HTTPS / managed TLS
   v
PulsePilot Web (public, free web service)
   |
   | Render private network
   v
PulsePilot API (private, starter service) ----+
                                               | private connection
PulsePilot Worker (starter worker) ------------+----> Render PostgreSQL + pgvector
```

- Frankfurt keeps all application and database services in one region.
- Only the Next.js service receives internet traffic. Browser authentication
  and backend calls continue through its server-side proxy.
- API startup and readiness remain independent from migration. The API's paid
  pre-deploy command runs EF Core migrations and the idempotent 100-feedback
  seed before a new revision becomes active.
- API and Worker receive Render's internal PostgreSQL URL. The infrastructure
  layer normalizes it to an Npgsql key/value connection string without logging
  credentials.
- PostgreSQL has no public IP allow-list entries. The Blueprint explicitly pins
  PostgreSQL 17 because the application migrations and pgvector baseline are
  tested against that major version.
- Render generates the JWT signing secret. The demo password and OpenAI API key
  are prompted secrets and never enter Git, Docker build arguments, or logs.

## Cost boundary

The committed Blueprint selects a free public Web service, a free 1 GB
PostgreSQL instance, and two Starter application instances for the private API
and continuously running Worker. Render free PostgreSQL expires after 30 days
and has no backups. Treat it as disposable portfolio data, then upgrade or
replace it before expiry. The Render creation screen is the final cost approval
point; syncing this file alone does not create resources.

## First deployment

1. Sign in to Render and choose **New > Blueprint**.
2. Connect `mehmetanil10/pulsepilot-ai`, select `main`, and keep
   `render.yaml` as the Blueprint path.
3. Review the displayed monthly estimate. Do not create the resources unless
   the Starter API and Worker cost is acceptable.
4. Supply the prompted values for both services:
   - `OpenAI__ApiKey`: the same restricted provider key for API and Worker;
   - `Seed__Password`: a unique 12-128 character demo password on the API.
5. Apply the Blueprint. Render creates PostgreSQL first, builds the shared
   API/Worker artifact, executes migration and seed, then activates API, Worker,
   and Web.
6. Record the Web `https://...onrender.com` URL. Do not expose the private API
   address or place provider/database secrets in repository variables.

The seeded login is `demo@pulsepilot.ai` plus the password entered during
Blueprint creation. Change the password in Render and redeploy the API whenever
the demo credential should rotate; the idempotent seeder updates the hash.

## Verification

Run the public, read-only smoke contract locally:

```powershell
./scripts/smoke-deployment.ps1 -BaseUrl https://your-service.onrender.com
```

Then open **Actions > Live demo smoke test > Run workflow**, paste the same
HTTPS origin, and retain the successful run as deployment evidence. The smoke
test verifies the Web health payload, login page, and response security headers
without creating users or changing demo data.

For the final product journey, sign in with the demo account, inspect the seeded
dashboard, submit a manual feedback item, wait for Worker processing, review the
analysis, approve its pending action, and confirm the resulting backlog item.

## Operational notes

- A free Web instance sleeps after 15 idle minutes, so the first request can
  take about a minute. The private API and Worker remain on paid instances.
- Automatic deployment uses `checksPass`; a failing GitHub CI run does not roll
  out. Render preserves the last successful revision when a build or pre-deploy
  command fails.
- Roll back the Web, API, and Worker from their Render Events pages to the same
  Git commit. Database migrations are forward-only; take a database backup
  before any future destructive schema change.
- OpenTelemetry export stays disabled until a managed OTLP endpoint and secret
  are selected. Render logs still receive structured, PII-redacted output.
- The current portfolio topology is single-instance and deliberately avoids
  Kubernetes, a public API edge, and multi-region state.
