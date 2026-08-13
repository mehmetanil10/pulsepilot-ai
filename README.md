# PulsePilot AI

PulsePilot AI is an AI-driven product feedback and engineering copilot for SaaS
teams. It is designed to analyze feedback, detect related reports, prioritize
issues, and keep critical actions behind human approval.

## Current status

Sprint 1 and Sprint 2 are complete, and Sprint 3 is in progress. The .NET 10
Clean Architecture foundation, core domain model, PostgreSQL persistence layer, JWT authentication,
workspace-isolated Feedback CRUD, API observability baseline, Docker development
stack, idempotent demo seed, and a Next.js 16 App Router frontend foundation are
ready. The web app includes a responsive product shell, login and registration
flows, HttpOnly JWT sessions, an allowlisted same-origin API gateway, and a
standalone production container. Its live dashboard now presents workspace-scoped
KPIs, trending issue momentum, processing health, recent feedback, pending AI
actions, and category distribution. Its searchable feedback library links to a
responsive detail experience that combines the original signal, AI summary,
structured metrics, processing information, cluster context, suggested action,
and semantic matches without exposing customer identity fields. The AI
intelligence foundation now includes provider-independent structured analysis
contracts, a persisted `FeedbackAnalysis` model, an OpenAI Responses API adapter
with strict Structured Outputs, and pgvector-backed feedback embeddings. A
separate database-backed worker now claims pending feedback with expiring leases, retries transient
failures, and atomically persists analysis plus embedding without holding a
transaction during either AI call. Cosine similarity search is workspace-scoped,
and category/component-aware cluster assignment groups related reports safely
across concurrent worker replicas. High-priority clusters now produce durable,
human-reviewable `PendingAction` recommendations. Approved engineering actions
execute the controlled `CreateBacklogItemTool`; approved customer-response
actions generate persisted, unsent drafts through `DraftCustomerResponseTool`.
`SearchSimilarFeedbackTool` exposes workspace-scoped semantic retrieval to the
agent runtime. `GenerateReportTool` now combines deterministic
workspace statistics and trends with a strictly validated AI narrative for
on-demand weekly product intelligence reports. A provider-neutral agent
orchestration runtime now supports bounded multi-turn execution, strict OpenAI
Responses function calling, and four allowlisted analytical tools while keeping
workspace identity under backend control. EF Core migrations and
Testcontainers-backed API, repository, seed, worker, and provider-contract
integration tests are included.

## Solution structure

- `PulsePilot.Domain`: domain entities and business rules
- `PulsePilot.Application`: use cases and application abstractions
- `PulsePilot.Infrastructure`: persistence and external service adapters
- `PulsePilot.Api`: ASP.NET Core HTTP API
- `PulsePilot.Worker`: durable background feedback analysis host
- `PulsePilot.Web`: Next.js App Router frontend and secure backend gateway
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

The stack contains the web app, API, background worker, and PostgreSQL 17 with
pgvector. A one-shot migration service must complete successfully before the API
and worker start; the web app waits for the API readiness check.

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
Invoke-RestMethod http://localhost:3000/api/health
```

PulsePilot Web is available at `http://localhost:3000`. Swagger UI is available
at `http://localhost:8080/swagger` when the environment
is `Development`. View logs or stop the stack with:

```powershell
docker compose logs --follow web api worker
docker compose down
```

`docker compose down --volumes` also permanently removes the local PostgreSQL
data volume.

The API uses JWT bearer authentication and an ephemeral Data Protection provider;
no cookie-encryption keys or application secrets are written into the image or
container filesystem.

## Web frontend

The frontend lives in `src/PulsePilot.Web` and requires Node.js 20.19 or newer
to satisfy the complete Next.js and ESLint toolchain.
For local development, copy its `.env.example` to `.env.local`, then run:

```powershell
npm --prefix src/PulsePilot.Web install
npm --prefix src/PulsePilot.Web run dev
```

The browser never receives the API access token in JavaScript-readable storage.
Login and registration Route Handlers keep it in a `Secure`, `HttpOnly`,
`SameSite=Lax` cookie in production. Authenticated browser requests use the
allowlisted `/api/backend/*` gateway, while the ASP.NET Core API remains the
authorization source of truth. Dashboard data is loaded only by the authenticated
server-rendered experience.

The live dashboard uses two authenticated, workspace-scoped API endpoints:

- `GET /api/dashboard/summary?periodDays=7`
- `GET /api/dashboard/trending?periodDays=7&limit=5`

The summary response includes feedback received since 00:00 UTC, analyzed
feedback and failures for the selected period, active P1 clusters, pending action
count, recent feedback metadata, pending-action previews, and category totals.
The frontend validates both API response shapes before rendering and supports
7, 30, and 90-day views without exposing the access token to browser code.

`GET /api/feedback` also powers the live feedback library. Its workspace-scoped
query supports source, processing status, category, component, severity,
sentiment, inclusive date ranges, title/content search, and pagination. List
responses include a compact AI-analysis projection without customer PII.
Each `/feedback/{id}` web route loads the original feedback and independently
aggregates analysis, cluster, and semantic-match data. Expected pending and stale
analysis states are rendered explicitly, while an unavailable auxiliary section
does not prevent the original signal from being inspected.

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

## Feedback Cluster API

Cluster endpoints require a bearer token and always derive the workspace from
validated JWT claims:

- `GET /api/clusters?page=1&pageSize=20`
- `GET /api/clusters/{id}?page=1&pageSize=20`

List results are ordered by active feedback count and recent cluster activity.
Cluster detail returns paginated, non-deleted feedback members. Cross-workspace
cluster identifiers return `404`.

## Pending Action API

Pending-action endpoints require a bearer token and are always workspace-scoped:

- `GET /api/actions?page=1&pageSize=20&status=pending`
- `GET /api/actions/{id}`
- `POST /api/actions/{id}/approve` (workspace admin only)
- `POST /api/actions/{id}/reject` (workspace admin only)

The worker deterministically recommends an action only for `P1` and `P2`
clusters. Bugs and feature requests create an engineering-issue recommendation,
`P1` complaints are escalated, and eligible complaints or questions create a
customer-response draft recommendation. Lower-priority and informational
clusters produce no action. The LLM's suggested action is retained only as
payload context and cannot select or execute a tool.

Recommendations are persisted with `Pending` status. A filtered PostgreSQL
unique index permits only one active (`Pending` or `Approved`) recommendation
for the same workspace, cluster, and action type. Workspace admins can explicitly
approve or reject a pending recommendation. Repeating the same decision is
idempotent, while an opposite or terminal decision returns `409`. Optimistic
concurrency and a database review-state constraint ensure concurrent requests
cannot produce conflicting decisions. Approving a `CreateEngineeringIssue`
recommendation invokes the controlled backend `CreateBacklogItemTool`, creates
one workspace-scoped backlog item, and atomically advances the action to
`Executed`. Action-level advisory locking and a unique source-action index make
concurrent repeated approvals idempotent. A filtered unique index also prevents
more than one `Open` or `InProgress` backlog item for the same source cluster.
Approving `DraftCustomerResponse` invokes the structured AI drafting contract,
persists exactly one workspace-scoped draft, and advances the action to
`Executed` in the same save. Provider failure leaves the action `Pending`, and
no draft is ever sent automatically. Action types whose tools are not yet
implemented remain `Approved`.

Generated drafts are available to authenticated members of the owning workspace:

- `GET /api/actions/{id}/customer-response-draft`

Cross-workspace action identifiers return `404`.

## Backlog API

Backlog endpoints require a bearer token and derive the workspace exclusively
from validated JWT claims:

- `GET /api/backlog?page=1&pageSize=20&status=open&priority=p1`
- `GET /api/backlog?sourcePendingActionId={actionId}`
- `GET /api/backlog/{id}`

Each backlog item records its source cluster, source pending action, approving
user, priority, and lifecycle status. New tool-created items start as `Open`.
Cross-workspace identifiers return `404`; list filters cannot expose another
workspace's records.

## Weekly Report API

Authenticated workspace members can generate an on-demand product intelligence
report without exposing raw feedback or customer PII to the reporting model:

- `POST /api/reports/weekly`

The optional body accepts `periodDays` and `trendingIssueLimit`; both are bounded
by configuration. The response includes the deterministic source statistics and
trending issues alongside a strict structured report containing a title,
executive summary, key insights, and recommended engineering priorities. The
tool retries only transient provider failures with a bounded timeout. Reports
are generated on demand and are not emailed, published, or persisted by this MVP
endpoint.

## Copilot API

Authenticated workspace members can ask natural-language questions through the
agent runtime:

- `POST /api/copilot/chat`

The request body contains only a required `message`; workspace context is derived
from validated JWT claims and cannot be selected by the client. Message length is
bounded by `AgentOrchestration:MaxUserMessageLength`. A successful response
contains the grounded answer, model-turn count, tool-call count, and a safe tool
usage summary containing only the tool name and success state. Provider call IDs,
tool arguments, raw protocol history, and workspace identifiers are not exposed.

The endpoint can invoke only the four analytical tools in the backend allowlist.
It cannot create backlog items, approve actions, or send customer responses.
Invalid requests return `400`, anonymous requests return `401`, invalid provider
output returns `502`, and unavailable or disabled providers return a generic `503`
Problem Details response.

## Agent tools

Agent tools are application-layer functions whose execution rules remain under
backend control. Tool inputs do not allow the model to supply a workspace,
similarity threshold, or embedding vector.

- `CreateBacklogItemTool` executes only an approved engineering action and is
  idempotent for repeated approvals.
- `DraftCustomerResponseTool` accepts only an approved customer-response action,
  requires completed structured feedback analysis, validates a strict AI output,
  and stores an empathetic draft of at most 120 words. It performs no email,
  messaging, or other external delivery operation.
- `SearchSimilarFeedbackTool` accepts a source feedback identifier and an
  optional bounded result limit. The orchestrator supplies the trusted workspace
  context, while the backend applies the configured similarity threshold and
  returns only completed, non-deleted matches. The source embedding must still
  match the feedback's current title and content.
- `GetFeedbackStatisticsTool` accepts an optional bounded lookback period. It
  returns total and analyzed feedback counts, average severity, and zero-filled
  processing-status, source, category, component, sentiment, and severity
  distributions. Statistics use feedback creation time and exclude soft-deleted
  or cross-workspace records.
- `GetTrendingIssuesTool` compares the current lookback window with the equally
  sized preceding window. It returns only clusters whose report volume grew,
  ordered by absolute increase, with current/previous counts, delta, percentage
  growth, priority, and an explicit marker for newly appearing issues.
- `GenerateReportTool` orchestrates `GetFeedbackStatisticsTool` and
  `GetTrendingIssuesTool`, sends only aggregate metrics to the AI provider, and
  validates a strict bounded result. Quantitative source data remains
  deterministic; the model supplies only the narrative synthesis and grounded
  engineering recommendations.

The existing `GET /api/feedback/{id}/similar` endpoint delegates to the same
search tool, keeping API and future agent behavior consistent.

## Agent orchestration

The application layer contains a provider-neutral `IAgentOrchestrator` and
`IAgentTurnClient` boundary. The orchestrator keeps the original user message,
allowlisted tool definitions, and validated tool exchanges across turns. The
trusted workspace identifier is supplied only to the backend tool executor and
is never included in the provider turn request.

Execution is bounded by configurable maximum turns, calls per turn, total tool
calls, argument/output sizes, final-answer size, and an overall timeout. Tool
names must match the backend catalog, call identifiers must be unique, and every
argument payload and schema must be a JSON object. Invalid provider turns fail
closed before any calls in that turn execute. The final turn cannot execute a
tool because no turn would remain to synthesize a user-facing answer.

The runtime exposes exactly four read-only or analytical functions:
`search_similar_feedback`, `get_feedback_statistics`, `get_trending_issues`, and
`generate_report`. Backlog creation and customer-response drafting remain behind
the existing human-approval workflow and are deliberately absent from the agent
allowlist. Input schemas follow OpenAI's
[strict function-calling requirements](https://developers.openai.com/api/docs/guides/function-calling):
all properties are required, nullable types represent optional values, and
additional properties are rejected.

The backend binds arguments case-sensitively, rejects missing, duplicate, unknown,
or out-of-range fields, and dispatches only known tool names with the trusted
workspace context. Expected domain failures become stable, non-sensitive error
objects. Similar-feedback content and list sizes are reduced before returning
results to the model.

The OpenAI Responses adapter sends `store: false`, replays function calls,
function outputs, and encrypted reasoning state in order, and wraps every tool
result with an explicit success marker. Its instructions treat user and tool
content as untrusted data and prohibit claims of side effects. Provider transport,
status, refusal, and timeout mapping is shared by structured AI calls and agent
turns. The authenticated Copilot endpoint maps the token workspace into this
runtime and returns only a safe answer and tool-usage summary.

## AI intelligence foundation

`ILLMClient` defines provider-independent structured analysis, embedding,
customer-response drafting, and product-report generation boundaries. The
analysis result contains category, component, severity, sentiment, summary,
suggested action, and confidence.
Application validation and domain invariants
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

`FeedbackCluster` groups feedback only when semantic similarity meets the
configured threshold and the structured category and component also match. The
worker joins the closest existing cluster or creates a new one. PostgreSQL
workspace advisory locks serialize only the short assignment-and-save section,
preventing duplicate clusters when worker replicas finish similar feedback at
the same time; provider calls remain outside the lock. Updating feedback clears
its stale cluster membership until reprocessing completes.

Cluster priority is deterministic and does not delegate business-critical
ranking to the LLM. The worker normalizes four factors to `0–1`, applies
configurable weights, and persists a `0–100` score plus a `P1`–`P4` level in the
same locked save as cluster assignment:

```text
score = 100 × (severity × 0.35 + frequency × 0.30
             + customerImpact × 0.20 + recency × 0.15)
```

Severity uses the highest current member severity. Frequency saturates at 20
active reports, customer impact counts distinct email/name identities and
saturates at 10, and recency is the share of reports created within the last 7
days. Anonymous reports receive distinct identities. Default thresholds are
`P1 >= 75`, `P2 >= 50`, `P3 >= 25`, otherwise `P4`. Weights, normalization
counts, the recency window, and thresholds are configurable through the
`PriorityScoring` section and corresponding `PRIORITY_*` Compose variables.
Cluster list results are ordered by priority score before feedback count and
activity; both cluster endpoints return `priorityScore` and `priority`.

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

## Sprint 2 acceptance

Sprint 2 is complete against its Definition of Done:

- New feedback is persisted as `Pending` and claimed by the background worker.
- Provider-independent structured analysis is validated and persisted.
- A 1,536-dimensional embedding is generated and stored in PostgreSQL/pgvector.
- Workspace-scoped semantic search returns related completed feedback.
- Similar category/component feedback joins a concurrency-safe cluster.
- Deterministic priority scoring persists a `0–100` score and `P1`–`P4` level.
- Transient provider failures and timeouts use bounded retries; permanent failures
  move feedback to `Failed` without partial analysis or embedding data.
- Failed records can be explicitly queued for retry through the API.

The Sprint 2 acceptance test exercises the complete API-to-worker-to-API flow
with a deterministic in-process provider: registration, feedback creation,
analysis, embedding, semantic similarity, clustering, priority calculation,
persistence, and workspace isolation. It performs no external AI calls. The
remaining provider-contract tests use a local HTTP stub.
