# PulsePilot Web

Next.js 16 App Router frontend for PulsePilot AI. It uses TypeScript, Tailwind
CSS, server-side JWT session handling, and a same-origin backend-for-frontend
gateway.

Use Node.js 20.19 or newer. The container build is pinned to Node.js 22.22.

## Local development

Copy `.env.example` to `.env.local`, start the PulsePilot API on port 8080, then:

```powershell
npm install
npm run dev
```

Open `http://localhost:3000`. The JWT returned by the API is stored only in an
HttpOnly cookie; browser code calls `/api/backend/*` instead of receiving the
token directly.

The dashboard reads `/api/dashboard/summary` and `/api/dashboard/trending`
directly from the API in a server component. Its 7, 30, and 90-day views are
always uncached so workspace metrics reflect the current backend state.

The feedback library uses the same server-only session path. It supports
workspace-scoped search, source, processing status, AI category, component,
severity, sentiment, date filters, and paginated links to feedback detail.
The detail route combines the original signal, current or stale AI analysis,
processing state, associated cluster, suggested action, and semantic matches.
Auxiliary failures are isolated so the original feedback remains readable.

## Checks

```powershell
npm run lint
npm test
npm run build
```
