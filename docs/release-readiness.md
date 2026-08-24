# Release readiness, known limitations, and roadmap

Sprint 4 Task 48 closes the local portfolio-readiness loop without presenting
PulsePilot as a production SaaS that it is not yet. The repository demonstrates
the product architecture, deterministic demo, quality gates, security baseline,
and human-control model. A publicly reachable deployment remains the explicit
Task 45 exit condition.

## Current release boundary

| Area | Evidence | Status |
| --- | --- | --- |
| Core feedback, analysis, clustering, priority, actions, and backlog | Unit and PostgreSQL integration suites | Ready |
| Authenticated Web product journey | Isolated Compose Playwright acceptance | Ready |
| Populated portfolio journey | Deterministic 100-feedback capture test and screenshots | Ready |
| AI analysis quality | Versioned bilingual golden dataset and evaluation runner | Ready for controlled evaluation |
| Containers and supply chain | Non-root/read-only validation, dependency audits, Trivy CI gates | Ready as a baseline |
| Public cloud availability | Render Blueprint exists; successful live URL and smoke evidence are not recorded yet | Pending Task 45 |

“Ready” means the committed contract is implemented and exercised. It does not
mean an SLA, compliance certification, unlimited scale, or autonomous execution.

## Known limitations

### Product and integrations

- Feedback enters through the authenticated API or deterministic seed. Native
  Zendesk, Intercom, Slack, app-store, and email connectors are not implemented.
- Approved engineering actions create PulsePilot backlog records. There is no
  outbound Jira/GitHub issue creation yet.
- Customer-response actions create unsent drafts by design. PulsePilot does not
  send email or publish a support reply automatically.
- Workspace authorization currently focuses on Admin and Member boundaries. It
  does not yet include enterprise SSO, SCIM, granular custom roles, or a complete
  invitation and access-review workflow.
- The UI covers the portfolio workflow but is not a complete feedback-operations
  console. Bulk triage, saved views, exports, source sync status, and lifecycle
  editing remain future product work.

### AI and evaluation

- OpenAI is the implemented live provider. There is no automatic provider
  fallback, multi-model routing, or budget-aware model selection.
- Deterministic tests and the golden dataset are release gates; live-provider
  evaluation remains opt-in because it costs money and can vary over time.
- Priority is deliberately deterministic after AI extraction. Its weights are a
  product policy baseline, not a universally validated prioritization formula.
- Copilot is request/response and workspace-grounded. It does not retain a
  durable cross-session conversation memory or autonomously schedule work.

### Operations and scale

- The free Render design sleeps when idle, has cold starts, and uses disposable
  PostgreSQL without backups. It is a portfolio environment, not a production SLA.
- The free profile hosts feedback processing in the API process. A production
  topology should restore the standalone Worker and independently scale it.
- OpenTelemetry instrumentation exists, but a collector, dashboards, alerts,
  retention policy, and on-call process must be supplied by the deployment.
- Performance measurements are local regression baselines. No public p95/p99,
  concurrency, availability, recovery-time, or recovery-point objective has been
  established for a deployed environment.
- The application has responsive layouts and automated Chromium coverage, but a
  full cross-browser, mobile-device, screen-reader, and WCAG audit is still due.

## Roadmap

### P0 — publish the portfolio demo

1. Complete Task 45 on Render and record the Web origin.
2. Run the read-only local and GitHub deployment smoke contracts.
3. Run the populated demo tour against the HTTPS origin.
4. Rotate the demo credential, document the database expiry date, and publish the
   final video without exposing secrets or provider dashboards.

### P1 — real feedback ingestion

- Add signed, idempotent webhook adapters for Zendesk and Intercom first.
- Add source cursor/replay state, dead-letter handling, and connector health UI.
- Map external identities to minimal internal references and define retention and
  deletion contracts before ingesting real customer data.
- Add CSV import as a controlled onboarding path.

### P2 — team workflow

- Add invitations, enterprise identity, granular roles, and an audit-log UI.
- Add bulk triage, saved filters, exports, action history, and lifecycle editing.
- Add allowlisted Jira/GitHub tools with the same approval and idempotency model.
- Add draft review and explicit send handoff to a support system; do not give the
  model direct outbound authority.

### P3 — AI reliability and governance

- Version prompts and schemas, schedule golden-dataset evaluations, and alert on
  quality, latency, token-cost, refusal, and tool-selection regressions.
- Add reviewed provider/model fallbacks with per-workspace budgets and kill switches.
- Add evidence citations and a human correction loop without training on customer
  data by default.

### P4 — production operations

- Use private paid compute, a standalone autoscaled Worker, durable queueing,
  managed backups, point-in-time recovery, and tested restore procedures.
- Establish environment-specific SLOs, capacity tests, dashboards, alerts,
  incident runbooks, and dependency failure drills.
- Complete threat modeling, data-processing documentation, accessibility review,
  and the legal/compliance work appropriate to the target customers.

## Final local E2E demo contract

The Task 48 Playwright tour signs into the deterministic demo workspace and
asserts the populated product story rather than merely checking page headings:

1. The 30-day dashboard shows 76 processed signals, seeded trends, and two
   pending recommendations.
2. The feedback library exposes 100 synthetic signals with a bounded first page.
3. The first signal exposes structured analysis, confidence, and cluster context.
4. The human-review queue contains the two seeded recommendations.
5. The backlog contains the traceable seeded engineering item.
6. Copilot presents read-only capabilities and the human-control boundary.
7. Sign-out returns the browser to the login page.

Run it against the current local Compose stack:

```powershell
$env:PULSEPILOT_DEMO_PASSWORD = "<your seeded demo password>"
./scripts/capture-demo.ps1
```

The same command accepts the future Render Web origin through `-BaseUrl`. A
successful local run is Task 48 evidence; a successful HTTPS run plus the public
smoke workflow is still required to close Task 45.
