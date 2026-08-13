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

## Checks

```powershell
npm run lint
npm test
npm run build
```
