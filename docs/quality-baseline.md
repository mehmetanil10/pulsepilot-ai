# Quality baseline

This document records the Sprint 4 quality baseline established in Task 34 and
ratcheted in Tasks 35 and 36. It is a regression floor, not the final coverage
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

Captured on 2026-08-21 in Release configuration with .NET SDK 10.0.303. All 259
quality-baseline tests and both Playwright acceptance scenarios passed; none were
skipped.

| Suite | Tests | Line coverage | Branch coverage | Function coverage | Statement coverage |
| --- | ---: | ---: | ---: | ---: | ---: |
| .NET unit | 125 | 69.34% | 75.63% | n/a | n/a |
| .NET Worker unit | 2 | 100.00% | 100.00% | n/a | n/a |
| .NET integration | 81 | 92.65% | 57.85% | n/a | n/a |
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

## Regression floors

`quality-gates.json` contains deliberately conservative, whole-percentage floors
just below the measured values. Test-count floors prevent silent deletion or
filtering of test cases, while coverage floors prevent meaningful regressions.

| Suite | Minimum tests | Minimum lines | Minimum branches | Minimum functions | Minimum statements |
| --- | ---: | ---: | ---: | ---: | ---: |
| .NET unit | 125 | 69% | 75% | n/a | n/a |
| .NET Worker unit | 2 | 100% | 100% | n/a | n/a |
| .NET integration | 81 | 92% | 57% | n/a | n/a |
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

## Remaining priorities after Task 36

- Server-rendered page/data modules and the larger dashboard, feedback, backlog,
  and actions views still have limited direct unit coverage; the main workspace
  behavior is currently protected by the browser journey.
- The browser suite covers authentication, feedback ingestion and inspection,
  empty action/backlog states, safe Copilot failure, and sign-out. Deterministic
  populated action-review and backlog UI journeys should follow the expanded demo
  seed work in Task 38.
- Branch coverage trails line coverage in the integration suite, so negative and
  authorization paths need targeted assertions rather than additional happy-path
  volume alone.
- Task 37 must run the versioned golden dataset against a configured provider and
  report strict/tolerant accuracy, text-concept recall, contract validity,
  latency, and cost without weakening the deterministic CI baseline.
