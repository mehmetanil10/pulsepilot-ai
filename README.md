# PulsePilot AI

PulsePilot AI is an AI-driven product feedback and engineering copilot for SaaS
teams. It is designed to analyze feedback, detect related reports, prioritize
issues, and keep critical actions behind human approval.

## Current status

Sprint 1 is complete and Sprint 2 is in progress. The .NET 10 Clean Architecture
foundation, core domain model, PostgreSQL persistence layer, JWT authentication,
workspace-isolated Feedback CRUD, API observability baseline, Docker development
stack, and idempotent demo seed are ready. The AI intelligence foundation now
includes provider-independent structured analysis contracts, a persisted
`FeedbackAnalysis` model, an OpenAI Responses API adapter with strict Structured
Outputs, and pgvector-backed feedback embeddings. A separate database-backed
worker now claims pending feedback with expiring leases, retries transient
failures, and atomically persists analysis plus embedding without holding a
transaction during either AI call. Cosine similarity search is workspace-scoped
and available through the Feedback API. EF Core migrations and
Testcontainers-backed API, repository, seed, worker, and provider-contract
integration tests are included.

## Solution structure

- `PulsePilot.Domain`: domain entities and business rules
- `PulsePilot.Application`: use cases and application abstractions
- `PulsePilot.Infrastructure`: persistence and external service adapters
- `PulsePilot.Api`: ASP.NET Core HTTP API
- `PulsePilot.Worker`: durable background feedback analysis host
- `PulsePilot.UnitTests`: domain and application tests
- `PulsePilot.IntegrationTests`: API and infrastructure tests

## Build

Install the .NET 10 SDK, then run:

```powershell
dotnet restore PulsePilot.sln
dotnet build PulsePilot.sln --configuration Release --no-restore
dotnet test PulsePilot.sln --configuration Release --no-build
```

The integration tests require a running Docker engine and create disposable
PostgreSQL containers automatically.

## Docker setup

The stack contains the API, background worker, and PostgreSQL 17 with pgvector.
A one-shot migration service must complete successfully before the API and
worker start.

Create the local environment file and replace the PostgreSQL and JWT placeholder
secrets. The JWT secret must contain at least 32 random bytes:

```powershell
Copy-Item .env.example .env
$secret = [Convert]::ToBase64String(
  [Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
# Set JWT_SECRET=$secret and choose a PostgreSQL password in .env.
```

Build and start the stack:

```powershell
docker compose up --build --detach
docker compose ps
Invoke-RestMethod http://localhost:8080/health/ready
```

Swagger UI is available at `http://localhost:8080/swagger` when the environment
is `Development`. View logs or stop the stack with:

```powershell
docker compose logs --follow api worker
docker compose down
```

`docker compose down --volumes` also permanently removes the local PostgreSQL
data volume.

The API uses JWT bearer authentication and an ephemeral Data Protection provider;
no cookie-encryption keys or application secrets are written into the image or
container filesystem.

## Demo seed data

Demo data is disabled by default. To create a demo owner, workspace, and at least
100 realistic feedback records, set these values in `.env` before starting the
stack:

```dotenv
SEED_DEMO_DATA=true
SEED_DEMO_EMAIL=demo@pulsepilot.ai
SEED_DEMO_PASSWORD=replace-with-at-least-12-characters
SEED_FEEDBACK_COUNT=100
```

The one-shot migration service applies migrations first and then seeds the demo
workspace. Re-running it with the same database updates the demo account when
needed and only adds feedback until the configured target is reached, so records
are not duplicated. The feedback count must be between 100 and 10,000. The demo
password is required only when seeding is enabled and must never be committed.

After the API becomes healthy, sign in through `POST /api/auth/login` with the
configured demo email and password. The generated feedback spans Payments,
Authentication, Dashboard, Mobile, Reporting, Performance, and Feature Requests,
with intentionally related reports for semantic search and later clustering work.

## API infrastructure

- FluentValidation validators are discovered automatically from the Application
  and API assemblies.
- Errors use RFC-compatible Problem Details responses and include a `traceId`.
- Serilog emits structured request and application logs to the console.
- Swagger UI is available at `/swagger` in the Development environment.
- `/health/live` checks process liveness; `/health/ready` also checks PostgreSQL.

## Authentication

Register and login endpoints issue signed JWT bearer tokens scoped to the user's
workspace and role:

- `POST /api/auth/register`
- `POST /api/auth/login`
- `GET /api/auth/me`

Provide the signing secret through configuration. Use at least 32 random bytes
and never commit a production secret:

```powershell
$env:Jwt__Secret = "replace-with-a-secure-random-secret"
```

## Feedback API

Feedback endpoints require a bearer token. Workspace and creator identifiers
come from validated token claims and are never accepted from the request body:

- `POST /api/feedback`
- `GET /api/feedback?page=1&pageSize=20&source=manual&processingStatus=pending`
- `GET /api/feedback/{id}`
- `PUT /api/feedback/{id}`
- `DELETE /api/feedback/{id}`
- `GET /api/feedback/{id}/analysis`
- `POST /api/feedback/{id}/analysis/retry`
- `GET /api/feedback/{id}/similar?limit=10`

Deletion is implemented as a soft delete. The MVP accepts `manual` and `api` as
creation sources; API enums use camel-case string values.

## AI intelligence foundation

`ILLMClient` defines provider-independent structured analysis and embedding
boundaries. The analysis result contains category, component, severity, sentiment, summary,
suggested action, and confidence. Application validation and domain invariants
reject unsupported enum values, severity outside 1–5, confidence outside 0–1,
and empty or oversized text before persistence.

`FeedbackAnalysis` stores one current analysis per workspace-scoped feedback.
Database foreign keys prevent an analysis from referencing feedback in another
workspace, and PostgreSQL check constraints mirror the structured result rules.

`FeedbackEmbedding` stores one 1,536-dimensional vector per workspace-scoped
feedback. PostgreSQL's `vector` extension and a cosine HNSW index support nearest
neighbor lookup. The API returns only completed, non-deleted feedback from the
same workspace whose similarity meets the configured threshold; the source
embedding must match the feedback's current title and content.

The Infrastructure adapter uses the official OpenAI .NET SDK and Responses API.
It requests a strict JSON Schema result, then deserializes and validates the
result again before returning it to the application. Provider refusal,
incomplete output, malformed JSON, contract violations, and transient HTTP
failures are represented by provider-neutral application errors. SDK request and
content logging is disabled so feedback text and API credentials are not written
to application logs.

OpenAI access is disabled by default. To enable it locally, set these values in
`.env`; never commit a real key:

```dotenv
OPENAI_ENABLED=true
OPENAI_API_KEY=replace-with-a-project-api-key
OPENAI_MODEL=gpt-5.6-luna
OPENAI_EMBEDDING_MODEL=text-embedding-3-small
OPENAI_EMBEDDING_DIMENSIONS=1536
SIMILARITY_THRESHOLD=0.80
FEEDBACK_PROCESSING_ENABLED=true
```

The worker validates processing and provider configuration at startup. Each
pending feedback row is claimed atomically with a unique processing lease so
multiple worker replicas cannot process the same active job. AI calls run
outside database transactions. Transient failures use bounded exponential
backoff with jitter; permanent failures move the feedback to `Failed`. Stale
leases are recovered after the configured threshold, while late results from an
expired lease are discarded. Analysis and embedding calls each use the same
bounded retry and timeout policy. A feedback row is marked `Completed` only after
both validated results are saved atomically.

Analysis state and the latest persisted result are available through:

- `GET /api/feedback/{id}/analysis`
- `POST /api/feedback/{id}/analysis/retry`
- `GET /api/feedback/{id}/similar?limit=10`

The retry endpoint only accepts failed processing records. Updating feedback
returns it to `Pending`; an older analysis remains visible with
`isCurrent: false` until the worker replaces it. Similarity search returns `409`
while the source embedding is missing or stale. The threshold defaults to `0.80`,
and result limits are validated from configuration.

## Database migrations

Provide the PostgreSQL connection string through configuration rather than
committing credentials:

```powershell
$env:ConnectionStrings__Database = "Host=localhost;Port=5432;Database=pulsepilot;Username=pulsepilot;Password=change-me"
```

Restore the repository-local EF Core tool and apply migrations:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update `
  --project src/PulsePilot.Infrastructure/PulsePilot.Infrastructure.csproj `
  --startup-project src/PulsePilot.Infrastructure/PulsePilot.Infrastructure.csproj `
  --context AppDbContext `
  --connection $env:ConnectionStrings__Database
```

The README will be expanded with architecture, Docker, API, AI evaluation, and
deployment documentation as the project progresses.

## Sprint 1 acceptance

The automated test suite verifies registration, login, JWT authorization,
workspace isolation, feedback create/list/detail/update/delete behavior,
PostgreSQL persistence, validation and Problem Details responses, seed
idempotency, and demo-account login. The Docker smoke flow verifies migration,
optional seeding, API readiness, and authenticated feedback access against the
Compose stack.
