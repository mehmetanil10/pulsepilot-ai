# Quality baseline

This document records the Sprint 4 quality baseline established in Task 34 and
ratcheted through Task 43. It is a regression floor, not the final coverage
target.

## Reproduce the baseline

Prerequisites are the .NET 10 SDK, Node.js 20.19 or newer, and a running Docker
engine. From the repository root, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\quality-baseline.ps1
```

The command performs a deterministic dependency restore, Release build, unit
test run, Docker-backed integration test run, web coverage run, and quality-gate
check. For a faster local rerun after dependencies and Release binaries are
already current, use `-SkipRestore -SkipBuild`. `-SkipIntegrationTests` is only
for partial local diagnostics; a complete baseline always includes integration
tests.

Generated TRX, Cobertura, LCOV, JSON summary, and aggregate baseline files are
written below `artifacts/quality` and deliberately ignored by Git.

The browser acceptance suite uses a separate, isolated Compose project:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\e2e-acceptance.ps1
```

Use `-SkipImageBuild` only when the application images are already current. The
script tears down its containers, network, and PostgreSQL volume even when a test
fails.

## Current measured baseline

Captured on 2026-08-22 in Release configuration with .NET SDK 10.0.303. All 301
quality-baseline tests and both Playwright acceptance scenarios passed; none were
skipped.

| Suite | Tests | Line coverage | Branch coverage | Function coverage | Statement coverage |
| --- | ---: | ---: | ---: | ---: | ---: |
| .NET unit | 149 | 70.98% | 78.34% | n/a | n/a |
| .NET Worker unit | 2 | 100.00% | 100.00% | n/a | n/a |
| .NET integration | 99 | 93.55% | 61.89% | n/a | n/a |
| Web unit/component | 51 | 38.90% | 80.10% | 68.45% | 38.90% |
| Browser acceptance | 2 | n/a | n/a | n/a | n/a |

Coverage percentages are suite-specific and must not be averaged together.

Task 35 added direct Worker loop coverage, a deterministic Sprint 3 API
acceptance flow, authenticated Next.js gateway tests, critical interactive
component tests, and a real Chromium workspace journey. Compared with the Task
34 starting point, the quality suite grew from 236 to 256 tests and web line
coverage rose from 21.83% to 38.90%.

Task 36 added three deterministic dataset-contract tests and ratcheted the
quality suite from 256 to 259 tests. The tests validate the 60-case bilingual AI
evaluation dataset without making provider calls, so product-code coverage is
unchanged by design.

Task 37 added nine deterministic runner and scoring tests and ratcheted the
quality suite from 259 to 268 tests. They cover strict/tolerant labels,
concept recall, failed-provider denominators, latency percentiles, gates, CLI
validation, replay execution, and safe JSON reporting.

Task 38 expanded the existing seed acceptance test without inflating the test
count. It now exercises the complete demo story through PostgreSQL and the API,
raising integration coverage from 92.65% lines / 57.85% branches to 92.88% lines
/ 58.91% branches.

Task 39 added five security-focused error-contract tests and ratcheted the suite
from 268 to 273 tests. Direct and real-HTTP coverage now verifies allow-listed
details, stable problem codes and types, trace correlation, safe malformed JSON,
authenticated missing routes, no-store/nosniff headers, and the absence of
exception messages or stack traces in unexpected `500` responses.

Task 40 added seven logging and retry tests and ratcheted the suite from 273 to
280 tests. The shared API/Worker redactor is exercised against sensitive property
names, nested objects, email addresses, bearer/JWT values, provider keys, and
credential-like query values while proving operational metadata remains useful.
Retry coverage now explicitly protects transient recovery, permanent one-attempt
failure, and configured attempt exhaustion for feedback processing, reports, and
customer-response drafting.

Task 41 added 17 deterministic telemetry tests and ratcheted the suite from 280
to 297 tests. Activity and meter listeners now protect PII-safe bounded tags,
all feedback-source mappings, success/failure/no-op spans, AI retry measurements,
human review decisions, host provider registration, and invalid OTLP endpoint
rejection. API and Worker share ASP.NET Core, HTTP, runtime, PostgreSQL, custom
trace, and metric instrumentation while OTLP export remains explicit opt-in.

Task 42 added four deterministic protection and performance tests and ratcheted
the suite from 297 to 301 tests. The API tests protect scoped security headers,
safe `429 ProblemDetails`, health-probe rate-limit bypass, and configured request
body limits. A PostgreSQL command-budget test proves the dashboard statistics
snapshot remains correct while using exactly two reader commands instead of the
previous eight sequential queries. Integration coverage increased to 93.55%
lines and 61.07% branches.

Task 43 keeps the 301-test code baseline unchanged and adds an executable
container-hardening contract plus the existing two-scenario browser acceptance
journey against the rebuilt production images. The new .NET health probe is
compiled with the solution and exercised by Docker until API readiness; the
image inspector protects non-root users, isolated networks, loopback ports,
read-only roots, dropped capabilities, PID limits, OCI metadata, exec-form
health checks, and the absence of embedded secret environment variables.

## Regression floors

`quality-gates.json` contains deliberately conservative, whole-percentage floors
just below the measured values. Test-count floors prevent silent deletion or
filtering of test cases, while coverage floors prevent meaningful regressions.

| Suite | Minimum tests | Minimum lines | Minimum branches | Minimum functions | Minimum statements |
| --- | ---: | ---: | ---: | ---: | ---: |
| .NET unit | 149 | 70% | 78% | n/a | n/a |
| .NET Worker unit | 2 | 100% | 100% | n/a | n/a |
| .NET integration | 99 | 93% | 60% | n/a | n/a |
| Web unit/component | 51 | 38% | 80% | 68% | 38% |

A failed command, failed test, missing report, lower test count, or coverage
value below a floor makes the quality command exit unsuccessfully. Floors should
only move upward as coverage improves.

## Coverage scope

- .NET coverage is isolated by responsibility: core unit measures Application
  and Domain, Worker unit measures the Worker host, and integration measures API,
  Application, Domain, and Infrastructure. This prevents an unrelated project
  reference from silently changing a suite's denominator.
- .NET reports exclude test assemblies, EF Core migration sources,
  compiler-generated code, generated code, explicitly excluded code, and
  automatic property bodies.
- Web coverage includes every executable `src/**/*.ts` and `src/**/*.tsx` file.
  Test files and type-only declarations are excluded. Untested App Router and
  component files therefore count as zero instead of disappearing from the
  denominator.
- Integration tests use disposable PostgreSQL Testcontainers and make no external
  AI provider calls.

## Remaining priorities after Task 43

- Server-rendered page/data modules and the larger dashboard, feedback, backlog,
  and actions views still have limited direct unit coverage; the main workspace
  behavior is currently protected by the browser journey.
- The browser suite covers authentication, feedback ingestion and inspection,
  empty action/backlog states, safe Copilot failure, and sign-out. The expanded
  seed now provides deterministic populated action-review and backlog data; a
  browser journey that consumes that seeded state remains a targeted follow-up.
- Branch coverage trails line coverage in the integration suite, so negative and
  authorization paths need targeted assertions rather than additional happy-path
  volume alone.
- Real provider baselines remain opt-in because they incur network cost and can
  vary over time. Approved model/prompt results should be stored as reviewed
  release evidence rather than weakening the deterministic CI gate.
- The Task 38 seed acceptance test now protects its deterministic distribution,
  idempotency, dashboard KPIs and trends, action-state mix, backlog item, and
  unsent customer-response draft independently from the immutable evaluation
  dataset.
- The local load baseline is intentionally not a universal service-level
  objective. Task 43 must repeat it against the hardened production containers,
  and Task 45 must establish environment-specific latency and capacity targets
  behind the deployed edge proxy.
