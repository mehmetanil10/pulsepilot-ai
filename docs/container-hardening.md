# Container production baseline

Sprint 4 Task 43 establishes the production container baseline for PulsePilot.
It hardens the image contents, runtime privileges, network boundaries, and
operator defaults without treating Docker Compose as a complete cloud security
boundary.

## Image baseline

- API, migration, and Worker use Microsoft's shell-less .NET chiseled runtime
  image. The previous runtime `curl` installation is removed.
- .NET SDK/runtime, Node.js, and PostgreSQL/pgvector base images are pinned by
  tag and digest. Dependency automation must review and refresh both values so
  releases remain reproducible without silently missing security updates.
- API readiness is checked by a dedicated framework-dependent .NET probe copied
  from its own build stage. It has a two-second timeout and adds no package or
  shell to the runtime image.
- API, migration, Worker, and Web all declare non-root users. Runtime images do
  not contain build SDKs, source trees, package-manager caches, or application
  secrets.
- The Web final stage refreshes Alpine security packages and removes npm, npx,
  Corepack, and Yarn. Those tools remain available only in the dependency and
  build stages; the standalone production server requires Node.js alone.
- Health checks use Docker exec form, and final images carry OCI source,
  revision, version, creation-time, title, and description labels.
- The one-shot migration has its own target and image. It does not inherit the
  long-running API health check.
- The Dockerfile's default `render-final` stage is the API artifact used by the
  free Render demo. Feedback processing is hosted in-process by the API only
  when its configuration enables it. Local Compose and production-style images
  continue to use the separate role-specific API and Worker stages.

## Compose runtime baseline

- `Production` is the default ASP.NET Core environment.
- Published PostgreSQL, API, and Web ports bind to `127.0.0.1` by default.
  Set `BIND_ADDRESS` explicitly only when an external ingress requires it.
- PostgreSQL is attached only to an internal `data` network. Web cannot reach
  the database directly; API and Worker bridge the `app` and `data` networks.
- Application containers use a read-only root filesystem, bounded writable
  `tmpfs` mounts, an init process, `no-new-privileges`, a PID limit, and
  `cap_drop: ALL`.
- PostgreSQL receives a PID limit, privilege-escalation protection, a persistent
  named volume, a readiness check, and a graceful shutdown window.
- Secrets remain runtime configuration and are excluded by `.dockerignore`.
  The JWT options validator rejects secrets shorter than 32 bytes. Production
  deployment must source database, JWT, and provider secrets from the selected
  platform's secret manager rather than commit an `.env` file.

## Verification

Measured on 2026-08-22 after a clean production Compose build:

| Image | Size | Declared user |
| --- | ---: | --- |
| API | 180.18 MB | `app` |
| Migration | 180.16 MB | `app` |
| Worker | 179.22 MB | `app` |
| Web | 227.77 MB | `nextjs` |

The API chiseled image was also executed with `/bin/sh` as its entrypoint and
correctly rejected the command because no shell is present.

With the production images built, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\validate-container-hardening.ps1
```

The validator resolves the effective Compose model and inspects every
application image. It fails on a missing network boundary, externally bound
default port, writable application root, retained Linux capability, unbounded
PID use, root image user, missing OCI metadata, shell-form health check, or
secret-like image environment variable.

For reproducible release metadata, CI supplies `BUILD_CREATED`,
`BUILD_REVISION`, `BUILD_VERSION`, and an immutable `IMAGE_TAG`. The Task 44
GitHub Actions workflow automates these values, validates the runtime contract,
and scans every final image for fixable critical/high vulnerabilities. A
deployment platform may impose tighter CPU, memory, replica, ingress, TLS, and
secret-store policies in Task 45.
