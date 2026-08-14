# Quality baseline

This document records the Sprint 4 Task 34 quality starting point. It is a
regression baseline, not the final coverage target. Task 35 will add tests for
the gaps identified here and ratchet the floors upward.

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

## Measured starting point

Captured on 2026-08-14 in Release configuration with .NET SDK 10.0.303. All 236
tests passed and none were skipped.

| Suite | Tests | Line coverage | Branch coverage | Function coverage | Statement coverage |
| --- | ---: | ---: | ---: | ---: | ---: |
| .NET unit | 122 | 69.34% | 75.63% | n/a | n/a |
| .NET integration | 80 | 92.65% | 57.85% | n/a | n/a |
| Web unit | 34 | 21.83% | 74.66% | 60.00% | 21.83% |

Coverage percentages are suite-specific and must not be averaged together.

## Regression floors

`quality-gates.json` contains deliberately conservative, whole-percentage floors
just below the measured values. Test-count floors prevent silent deletion or
filtering of test cases, while coverage floors prevent meaningful regressions.

| Suite | Minimum tests | Minimum lines | Minimum branches | Minimum functions | Minimum statements |
| --- | ---: | ---: | ---: | ---: | ---: |
| .NET unit | 122 | 69% | 75% | n/a | n/a |
| .NET integration | 80 | 92% | 57% | n/a | n/a |
| Web unit | 34 | 21% | 74% | 60% | 21% |

A failed command, failed test, missing report, lower test count, or coverage
value below a floor makes the quality command exit unsuccessfully. Floors should
only move upward as coverage improves.

## Coverage scope

- .NET coverage includes loaded `PulsePilot.*` product assemblies and excludes
  test assemblies, EF Core migration sources, compiler-generated code, generated
  code, explicitly excluded code, and automatic property bodies.
- Web coverage includes every executable `src/**/*.ts` and `src/**/*.tsx` file.
  Test files and type-only declarations are excluded. Untested App Router and
  component files therefore count as zero instead of disappearing from the
  denominator.
- Integration tests use disposable PostgreSQL Testcontainers and make no external
  AI provider calls.

## Task 35 priorities exposed by the baseline

- The Worker host is not directly covered because no test project currently
  loads its host assembly.
- Most Next.js routes, server-rendered pages, client components, and authenticated
  gateway handlers have no direct coverage.
- There is no browser-level end-to-end acceptance suite for login, dashboard,
  feedback, action review, backlog, and Copilot journeys.
- Branch coverage trails line coverage in the integration suite, so negative and
  authorization paths need targeted assertions rather than additional happy-path
  volume alone.
