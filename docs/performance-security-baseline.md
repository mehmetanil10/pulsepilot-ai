# Performance and security baseline

This document records the Sprint 4 Task 42 baseline. The measurements are local
regression evidence, not a cloud capacity promise or an internet-facing DDoS
guarantee.

## Measured baseline

Captured on 2026-08-21 with the production Docker images running on Docker
Desktop. The load runner used Node.js `fetch`, consumed every response body, and
applied a five-second per-request timeout.

| Scenario | Requests | Concurrency | Result | Throughput | p95 latency |
| --- | ---: | ---: | --- | ---: | ---: |
| Liveness hot path | 1,000 | 50 | 100% HTTP 200 | 773.16 req/s | 122.07 ms |
| Anonymous API protection | 300 | 50 | 240 HTTP 401, 60 HTTP 429 | 457.20 req/s | 191.13 ms |

The committed liveness acceptance gate is 100% success and at most 150 ms p95
on the same local profile. Run it against a started Compose stack:

```powershell
node .\scripts\performance-baseline.mjs `
  --base-url http://localhost:8080 `
  --path /health/live `
  --requests 1000 `
  --concurrency 50 `
  --warmup 20 `
  --max-p95-ms 150 `
  --min-success-rate 1
```

For an authenticated endpoint, put a short-lived token in the process-only
`PULSEPILOT_BEARER_TOKEN` environment variable and select the endpoint with
`--path`. The runner never prints the token or response body.

## Bottleneck review

The dashboard feedback-statistics repository was the clearest database hot
path. A single snapshot executed eight sequential aggregate queries for totals,
processing states, sources, categories, components, sentiments, and severities.
Task 42 replaces them with two PostgreSQL conditional-aggregate queries:

1. feedback total, processing status, and source;
2. analyzed total, average severity, category, component, sentiment, and
   severity distribution.

A `DbCommandInterceptor` integration test seeds the complete 100-feedback demo
scenario, verifies the returned snapshot, and fails if the repository exceeds
two reader commands. Pagination remains capped at 100 records and read-only
repository paths continue to use `AsNoTracking`.

## API protection baseline

- A global fixed-window limiter protects `/api` and partitions authenticated
  traffic by workspace/user. Anonymous traffic is partitioned by connection IP.
- Authentication and AI/report endpoints have additional, stricter named
  policies. Rejections return a safe RFC 7807-compatible `429` response with a
  `Retry-After` header and trace correlation.
- Liveness/readiness probes bypass rate limiting so orchestration health checks
  cannot consume application capacity.
- Kestrel and IIS request bodies are capped at 64 KiB by default. Application
  validators retain narrower field-specific limits.
- API and health responses add `no-store`, CSP, permissions policy,
  `Referrer-Policy`, `nosniff`, and clickjacking protection. Swagger remains
  usable in Development because the API-only CSP is not applied to its UI.
- Limits are validated at host startup and can be overridden through the
  documented `ApiProtection` environment settings.

## Security boundary decisions

Application response compression is deliberately not enabled for authenticated
JSON. Compression over HTTPS can create side-channel risk, and an edge proxy is
better positioned to apply selective compression to public static assets.

TLS termination, HSTS, trusted forwarded-header configuration, WAF/bot controls,
and volumetric DDoS protection belong at the deployment edge. The application
limiter is defense in depth; it is not a replacement for those services. A
deployment behind a reverse proxy must configure trusted proxy addresses before
using forwarded client IPs for anonymous partitions.

The dependency audit must remain clean, and the existing controls still apply:
parameterized EF Core queries, workspace-scoped repositories, bounded paging,
JWT validation, generic authentication failures, PII-redacted logs, disabled
provider payload logging, and explicit human approval for side-effecting AI
actions.
