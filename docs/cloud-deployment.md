# Cloud deployment and live demo

Sprint 4 Task 45 defines a Render Blueprint for the public portfolio demo. The
repository remains the source of truth: Render deploys only commits whose
GitHub checks pass and builds the same production Dockerfiles exercised by CI.

## Topology

```text
Internet
   +---- HTTPS ----> PulsePilot Web (free web service)
   |                      |
   |                      | managed public HTTPS
   |                      v
   +--------------> PulsePilot API (free web service)
                           |  + in-process feedback worker
                           |
                           | private datastore connection
                           v
                    Render PostgreSQL + pgvector (free)
```

- Frankfurt keeps all application and database services in one region.
- Browser authentication and backend calls continue through the Next.js
  server-side proxy. The proxy receives the API's `RENDER_EXTERNAL_URL` through
  a Blueprint service reference because free Web services cannot receive
  private-network traffic.
- The demo API runs EF Core migrations and the idempotent 100-feedback seed
  during startup, then continues serving HTTP. The production-style migration
  container still exits after initialization by default.
- The API receives Render's internal PostgreSQL URL. The infrastructure layer
  normalizes it to an Npgsql key/value connection string without logging
  credentials.
- The demo API hosts the feedback analysis worker in-process. Docker Compose
  and the role-specific production images retain the standalone Worker.
- PostgreSQL has no public IP allow-list entries. The Blueprint explicitly pins
  PostgreSQL 17 because the application migrations and pgvector baseline are
  tested against that major version.
- Render generates the JWT signing secret. The demo password and OpenAI API key
  are prompted secrets and never enter Git, Docker build arguments, or logs.

## Cost boundary

The committed Blueprint selects two free Web services and one free 1 GB
PostgreSQL instance, so Render does not require a paid compute plan for the demo.
OpenAI API usage remains separately billable to the provider key owner. Render
free PostgreSQL expires after 30 days and has no backups. Treat it as disposable
portfolio data, then recreate, upgrade, or replace it before expiry.

Render grants a shared monthly pool of free instance hours. Both Web services
sleep after 15 idle minutes and can take about a minute to wake. This profile is
appropriate for a portfolio demo, not a production SLA.

## First deployment

1. Sign in to Render and choose **New > Blueprint**.
2. Connect `mehmetanil10/pulsepilot-ai`, select `main`, and keep
   `render.yaml` as the Blueprint path.
3. Confirm that both application services and PostgreSQL show the Free plan.
4. Supply the two prompted API values:
   - `OpenAI__ApiKey`: a restricted, project-scoped provider key;
   - `Seed__Password`: a unique 12-128 character demo password on the API.
5. Apply the Blueprint. Render creates PostgreSQL, builds both Web services, and
   the API performs migration plus seed before accepting traffic.
6. Record the Web `https://...onrender.com` URL. The API also has a managed
   public origin required by the free topology; keep application endpoints
   authenticated and never place provider/database secrets in the repository.

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
dashboard, submit a manual feedback item, wait for analysis processing, review the
analysis, approve its pending action, and confirm the resulting backlog item.

## Operational notes

- Both free Web instances sleep after 15 idle minutes. The first Web request and
  its first API call can therefore each incur a cold-start delay.
- Automatic deployment uses `checksPass`; a failing GitHub CI run does not roll
  out. Render preserves the last successful revision when a build fails.
- Roll back Web and API from their Render Events pages to the same Git commit.
  Database migrations are forward-only. The free database has no managed
  backups, so avoid destructive migrations in this disposable demo profile.
- OpenTelemetry export stays disabled until a managed OTLP endpoint and secret
  are selected. Render logs still receive structured, PII-redacted output.
- The current portfolio topology is single-instance and deliberately avoids
  Kubernetes and multi-region state. Move the Worker back to its standalone
  service and place the API on private paid compute before treating it as a
  production deployment.
