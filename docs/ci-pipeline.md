# Continuous integration pipeline

Sprint 4 Task 44 turns the local quality and container contracts into required,
repeatable GitHub Actions evidence. The workflow runs on pushes and pull
requests targeting `main`, supports manual dispatch, cancels superseded runs on
the same ref, and grants only read access to repository contents.

## Quality baseline job

The `Quality baseline` job runs on Ubuntu 24.04 with the repository-pinned .NET
SDK and Node.js 22.22.0. It:

1. restores deterministic NuGet and npm dependencies;
2. builds the complete Release solution;
3. runs 149 core unit, 2 Worker unit, 105 PostgreSQL integration, and 55 Web
   tests with the committed coverage floors;
4. verifies .NET formatting and zero-warning ESLint output;
5. queries NuGet advisories through machine-readable JSON and fails on any
   vulnerable direct or transitive package;
6. runs the full npm advisory gate at high severity;
7. uploads TRX, Cobertura, V8, and vulnerability reports for 14 days.

No real provider key is configured and no external AI request is made.
Integration databases are disposable Testcontainers instances.

## Production container job

The `Production containers` job starts only after the quality job passes. It:

1. builds the digest-pinned API, migration, Worker, and Web images with commit,
   run, and creation metadata; builds are deliberately sequential to stay below
   GitHub-hosted runner memory limits, and Next.js page generation is capped at
   two workers; the Web runtime also refreshes Alpine security packages and
   removes package managers that are unnecessary after the standalone build;
   CI additionally builds the combined Render API/Worker artifact that the
   platform creates from the Dockerfile's default stage;
2. executes the Compose/image hardening validator;
3. starts the isolated production stack and completes the two-scenario Chromium
   acceptance journey;
4. retains Playwright traces, screenshots, video, HTML output, and Compose logs
   when failures occur; plain-text image build logs and their final error context
   are retained independently;
5. scans every role-specific image and the Render deployment artifact with
   Trivy and fails for fixable critical or high
   vulnerabilities;
6. uploads the SARIF and E2E evidence for 14 days.

The job uses synthetic, run-local database and JWT values. They are not product
credentials and production secrets are never required by CI.

## Supply-chain controls

- Every referenced Action is pinned to a full commit SHA, with its release tag
  retained as a review comment.
- Workflow permissions default to `contents: read`; no package, deployment,
  pull-request, identity-token, or security-event write permission is granted.
- Dependabot checks GitHub Actions, NuGet, npm, root Docker, and Web Docker
  dependencies weekly and groups ecosystem updates for review.
- The job produces artifacts but does not publish images or deploy. Registry
  authentication, provenance/attestation, environment approval, and cloud
  rollout belong to Render's Blueprint boundary. The Blueprint deploys only
  commits whose linked checks pass.

The separate `Live demo smoke test` workflow is manually dispatched with an
HTTPS Web origin after a rollout. It validates public health, login rendering,
and security headers without receiving a cloud credential or mutating demo
data. Its `live-demo` GitHub environment provides an auditable deployment check
without granting the workflow permission to create or modify Render resources.

## Repository setting

After the first successful run, configure the `main` branch ruleset in GitHub to
require these status checks before merge:

- `Quality baseline`
- `Production containers`

Also require pull requests, block force pushes and branch deletion, and dismiss
stale approvals when protected files change. Repository rules are an external
administrative setting and therefore are documented rather than mutated by the
workflow.
