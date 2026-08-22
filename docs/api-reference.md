# PulsePilot API reference

The ASP.NET Core API exposes workspace-scoped feedback, analysis, prioritization,
human review, backlog, reporting, and copilot capabilities. Controller contracts
are the source of truth; Swagger provides an interactive development view.

## Base URL and Swagger

| Environment | Base URL |
| --- | --- |
| Local Docker | `http://localhost:8080` |
| Render demo | The managed `pulsepilot-api` HTTPS URL shown by Render |

Swagger UI is enabled only in `Development` at `/swagger`. The OpenAPI document
is available there at `/swagger/v1/swagger.json`. Production callers should use
the documented JSON contracts rather than depending on Swagger UI availability.

## Authentication

Registration and login return a signed bearer token. All endpoints except
registration, login, and health require:

```http
Authorization: Bearer <accessToken>
```

Registration creates a workspace and its first `Admin` user:

```http
POST /api/auth/register
Content-Type: application/json

{
  "email": "owner@example.com",
  "displayName": "Demo Owner",
  "password": "replace-with-a-strong-password",
  "workspaceName": "Example Product"
}
```

Login accepts:

```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "owner@example.com",
  "password": "replace-with-a-strong-password"
}
```

Both responses contain `accessToken`, `tokenType`, `expiresAt`, user identity,
workspace identity/name, and role. Do not log or persist the bearer token in
browser-readable storage. PulsePilot Web keeps it in an HttpOnly cookie.

## Conventions

- Request and response bodies use JSON and camel-case enum strings.
- Identifiers are UUID values.
- Pagination defaults to `page=1&pageSize=20` where supported.
- Workspace and acting-user identifiers always come from JWT claims.
- Cross-workspace identifiers intentionally return `404` instead of revealing
  whether another workspace owns the resource.
- Date filters use ISO `YYYY-MM-DD` values and inclusive bounds.
- Successful deletion returns `204 No Content`.
- Validation, domain, authentication, provider, and unexpected failures use
  `application/problem+json`.

## Endpoint catalog

### Health and authentication

| Method | Path | Authorization | Purpose |
| --- | --- | --- | --- |
| `GET` | `/health/live` | Anonymous | Process liveness |
| `GET` | `/health/ready` | Anonymous | Readiness including PostgreSQL |
| `POST` | `/api/auth/register` | Anonymous | Create workspace and admin; returns `201` |
| `POST` | `/api/auth/login` | Anonymous | Authenticate and issue JWT |
| `GET` | `/api/auth/me` | Member | Return current claim-backed identity |

### Dashboard and feedback

| Method | Path | Authorization | Purpose |
| --- | --- | --- | --- |
| `GET` | `/api/dashboard/summary?periodDays=7&recentFeedbackLimit=5&pendingActionLimit=4` | Member | KPIs, categories, recent feedback and pending actions |
| `GET` | `/api/dashboard/trending?periodDays=7&limit=5` | Member | Current-versus-previous issue momentum |
| `POST` | `/api/feedback` | Member | Create pending feedback; returns `201` |
| `GET` | `/api/feedback` | Member | Filtered, paginated feedback list |
| `GET` | `/api/feedback/{id}` | Member | Feedback detail |
| `PUT` | `/api/feedback/{id}` | Member | Update feedback and queue fresh analysis |
| `DELETE` | `/api/feedback/{id}` | Member | Soft-delete feedback |
| `GET` | `/api/feedback/{id}/analysis` | Member | Processing state and latest analysis |
| `POST` | `/api/feedback/{id}/analysis/retry` | Member | Requeue a failed analysis |
| `GET` | `/api/feedback/{id}/similar?limit=10` | Member | Workspace-scoped semantic matches |

Feedback list filters are `page`, `pageSize`, `source`, `processingStatus`,
`category`, `component`, `severity`, `sentiment`, `dateFrom`, `dateTo`, and
`search`. The MVP accepts `manual` and `api` as creation sources.

Example creation request:

```http
POST /api/feedback
Authorization: Bearer <accessToken>
Content-Type: application/json

{
  "title": "Checkout freezes after payment",
  "content": "Customers report that checkout remains on the loading state after a successful card authorization.",
  "source": "manual",
  "customerName": "Example Customer",
  "customerEmail": "customer@example.com"
}
```

Customer name and email are optional and remain protected fields. Creating or
updating feedback produces a `pending` processing state; analysis is asynchronous.

### Clusters, actions, and backlog

| Method | Path | Authorization | Purpose |
| --- | --- | --- | --- |
| `GET` | `/api/clusters?page=1&pageSize=20` | Member | Priority-ordered cluster list |
| `GET` | `/api/clusters/{id}?page=1&pageSize=20` | Member | Cluster detail and members |
| `GET` | `/api/actions?page=1&pageSize=20&status=pending` | Member | Human-review queue |
| `GET` | `/api/actions/{id}` | Member | Recommendation and bounded evidence |
| `POST` | `/api/actions/{id}/approve` | Admin | Approve and execute an allowlisted action |
| `POST` | `/api/actions/{id}/reject` | Admin | Reject a pending action |
| `GET` | `/api/actions/{id}/customer-response-draft` | Member | Read a generated, unsent draft |
| `GET` | `/api/backlog?page=1&pageSize=20&status=open&priority=p1` | Member | Filtered backlog list |
| `GET` | `/api/backlog/{id}` | Member | Backlog item detail |

Backlog lists also accept `sourcePendingActionId`. Approval and rejection are
idempotent for the same terminal decision; conflicting or invalid transitions
return `409`. Customer response drafts are persisted but never sent.

### Reports and copilot

| Method | Path | Authorization | Purpose |
| --- | --- | --- | --- |
| `POST` | `/api/reports/weekly` | Member | Generate bounded product intelligence from aggregate data |
| `POST` | `/api/copilot/chat` | Member | Run the bounded, read-only analytical agent |

Weekly report request values are optional and configuration-bounded:

```json
{
  "periodDays": 7,
  "trendingIssueLimit": 5
}
```

Copilot accepts only a message:

```json
{
  "message": "Which product issues grew fastest this week, and what should engineering investigate first?"
}
```

The copilot response includes the grounded answer, turn count, tool-call count,
and a safe tool-usage summary. It cannot approve actions, create backlog items,
or send customer responses. AI-backed report, copilot, and approval flows can
return `502` for invalid upstream output and `503` when the provider is disabled
or unavailable.

## Problem Details contract

Failures use a stable RFC Problem Details shape:

```json
{
  "type": "https://pulsepilot.ai/problems/validation_error",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more request values are invalid.",
  "instance": "/api/feedback",
  "code": "validation_error",
  "traceId": "request-correlation-id",
  "errors": {
    "content": ["Feedback content is required."]
  }
}
```

Common codes include `validation_error`, `authentication_required`,
`access_denied`, `not_found`, `conflict`, `business_rule_violation`,
`upstream_error`, `service_unavailable`, and `internal_error`. Public messages
are allowlisted and never expose exception text, stack traces, credentials, or
provider payloads. Error responses are marked `no-store` and `nosniff`.

## Rate and request protection

- Authentication endpoints use the strict authentication policy.
- AI-backed copilot and report endpoints use the AI policy.
- All other API endpoints use the general fixed-window policy.
- Oversized request bodies are rejected before controller execution.
- A rejected rate-limited request returns safe `429` Problem Details.

Limits are configured through `ApiProtection` and can be overridden by
environment variables without changing endpoint contracts.

## Contract verification

Controller integration tests exercise authentication, workspace isolation,
validation, status codes, Problem Details, AI/provider mappings, idempotency,
and the complete feedback-to-action flow. Run the full baseline with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\quality-baseline.ps1
```

See [architecture.md](architecture.md) for component and trust boundaries.
