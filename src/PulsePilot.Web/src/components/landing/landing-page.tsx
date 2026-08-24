import Image from "next/image";
import Link from "next/link";

import { PulseMark } from "@/components/brand/pulse-mark";

import styles from "./landing-page.module.css";

type LandingPageProps = {
  authenticated: boolean;
};

const capabilities = [
  {
    index: "01",
    title: "Unify customer signals",
    copy: "Accept feedback through an ingestion-ready API and preserve its source, identity, and workspace context.",
  },
  {
    index: "02",
    title: "Create structured intelligence",
    copy: "Extract sentiment, category, urgency, themes, and product impact from unstructured customer language.",
  },
  {
    index: "03",
    title: "Keep humans in control",
    copy: "Review every consequential tool action before it becomes an engineering backlog item.",
  },
];

const proofPoints = [
  ["Workspace isolation", "Tenant-aware access boundaries across product data"],
  ["Human approval", "Explicit review gates before consequential actions"],
  ["Observable workflows", "Structured logs, metrics, tracing, and health checks"],
  ["Production pipeline", "Automated build, test, security scan, and container checks"],
];

function ArrowIcon() {
  return <svg viewBox="0 0 20 20" aria-hidden="true"><path d="M4 10h11M11 6l4 4-4 4" /></svg>;
}

function CheckIcon() {
  return <svg viewBox="0 0 20 20" aria-hidden="true"><path d="m4 10 4 4 8-9" /></svg>;
}

function ProductFrame({ src, alt, priority = false, label }: {
  src: string;
  alt: string;
  priority?: boolean;
  label: string;
}) {
  return (
    <figure className={styles.productFrame}>
      <div className={styles.frameBar} aria-hidden="true">
        <span /><span /><span /><small>{label}</small>
      </div>
      <Image src={src} alt={alt} width={1440} height={900} priority={priority} sizes="(max-width: 900px) 94vw, 70vw" />
    </figure>
  );
}

export function LandingPage({ authenticated }: LandingPageProps) {
  const primaryHref = authenticated ? "/dashboard" : "/login";
  const primaryLabel = authenticated ? "Open dashboard" : "Explore the demo";

  return (
    <main className={styles.landing}>
      <header className={styles.header}>
        <Link className={styles.brandLink} href="/" aria-label="PulsePilot home"><PulseMark /></Link>
        <nav className={styles.nav} aria-label="Primary navigation">
          <a href="#product">Product</a><a href="#workflow">Workflow</a><a href="#architecture">Architecture</a>
        </nav>
        <div className={styles.headerActions}>
          {!authenticated && <Link className={styles.signIn} href="/login">Sign in</Link>}
          <Link className={styles.headerCta} href={primaryHref}>{primaryLabel}<ArrowIcon /></Link>
        </div>
      </header>

      <section className={styles.hero}>
        <div className={styles.heroGlow} aria-hidden="true" />
        <div className={styles.heroCopy}>
          <p className={styles.kicker}><span /> AI product intelligence · Human control</p>
          <h1>From customer signals to <em>accountable</em> engineering action.</h1>
          <p className={styles.heroLead}>PulsePilot turns fragmented product feedback into structured insight, reviewable AI actions, and a traceable engineering backlog—without taking humans out of the decision loop.</p>
          <div className={styles.heroActions}>
            <Link className={styles.primaryButton} href={primaryHref}>{primaryLabel}<ArrowIcon /></Link>
            <a className={styles.secondaryButton} href="#product">See how it works</a>
          </div>
          <ul className={styles.trustList} aria-label="Product principles">
            <li><CheckIcon /> Structured feedback intelligence</li>
            <li><CheckIcon /> Human-reviewed tools</li>
            <li><CheckIcon /> Evidence-linked decisions</li>
          </ul>
        </div>
        <div className={styles.heroVisual}>
          <div className={styles.heroBadge}><span>Live workspace</span><strong><i /> 100 demo signals</strong></div>
          <ProductFrame src="/landing/dashboard.png" alt="PulsePilot dashboard showing product feedback health, sentiment, and trending themes" label="pulsepilot / dashboard" priority />
          <div className={styles.signalCard}><span>Signal processed</span><strong>Mobile checkout friction</strong><small>P1 · Negative · Billing</small></div>
        </div>
      </section>

      <section className={styles.sourceRail} aria-label="Feedback sources">
        <p>One intelligence layer for every customer signal</p>
        <div><span>Support tickets</span><i /><span>Product reviews</span><i /><span>Surveys</span><i /><span>In-app feedback</span><i /><span>Ingestion API</span></div>
      </section>

      <section className={styles.problemSection} id="workflow">
        <div className={styles.sectionHeading}>
          <p className={styles.eyebrow}>The feedback-to-action gap</p>
          <h2>Your customers are already telling you what to build next.</h2>
          <p>The hard part is turning thousands of disconnected comments into decisions your product and engineering teams can trust.</p>
        </div>
        <div className={styles.capabilityGrid}>
          {capabilities.map((capability) => <article key={capability.index}><span>{capability.index}</span><h3>{capability.title}</h3><p>{capability.copy}</p></article>)}
        </div>
      </section>

      <section className={styles.productSection} id="product">
        <div className={styles.productIntro}>
          <div><p className={styles.eyebrow}>A shared product truth</p><h2>See the signal before it becomes noise.</h2></div>
          <p>One workspace connects portfolio-level health with the individual customer evidence behind every trend.</p>
        </div>
        <ProductFrame src="/landing/dashboard.png" alt="PulsePilot live dashboard with customer feedback analytics" label="workspace overview" />
        <div className={styles.metricRail}>
          <article><strong>100</strong><span>Deterministic demo signals</span></article>
          <article><strong>5</strong><span>Sentiment and urgency levels</span></article>
          <article><strong>1</strong><span>Traceable source of truth</span></article>
          <article><strong>0</strong><span>Silent autonomous actions</span></article>
        </div>
      </section>

      <section className={styles.analysisSection}>
        <div className={styles.analysisCopy}>
          <p className={styles.eyebrow}>Structured feedback analysis</p>
          <h2>Go from “customers are unhappy” to a decision-ready brief.</h2>
          <p>PulsePilot keeps the original voice of the customer beside the AI interpretation, so teams can inspect—not merely accept—the result.</p>
          <ul>
            <li><CheckIcon /><span><strong>Sentiment and urgency</strong> scored as explicit, inspectable fields.</span></li>
            <li><CheckIcon /><span><strong>Category and themes</strong> turn narrative feedback into a filterable dataset.</span></li>
            <li><CheckIcon /><span><strong>Confidence and rationale</strong> expose why a recommendation was made.</span></li>
          </ul>
        </div>
        <ProductFrame src="/landing/feedback-analysis.png" alt="PulsePilot feedback detail page with sentiment, urgency, summary, and extracted themes" label="feedback / structured analysis" />
      </section>

      <section className={styles.controlSection}>
        <div className={styles.controlHeader}>
          <div><p className={styles.eyebrowLight}>Human-in-the-loop by design</p><h2>AI proposes. Your team decides.</h2></div>
          <p>Consequential tool calls wait behind an explicit approval gate. Every action carries its rationale, payload, source feedback, and audit trail.</p>
        </div>
        <div className={styles.controlFlow} aria-label="Human review workflow">
          <span><i>01</i>Copilot proposes</span><b>→</b><span><i>02</i>Human reviews</span><b>→</b><span><i>03</i>Action executes</span><b>→</b><span><i>04</i>Audit persists</span>
        </div>
        <ProductFrame src="/landing/human-review.png" alt="PulsePilot human review screen for approving or rejecting an AI-proposed action" label="action review / approval required" />
      </section>

      <section className={styles.copilotSection}>
        <div className={styles.copilotVisual}><ProductFrame src="/landing/workspace-copilot.png" alt="PulsePilot workspace copilot answering questions using product feedback evidence" label="copilot / workspace intelligence" /></div>
        <div className={styles.copilotCopy}>
          <p className={styles.eyebrow}>Grounded product copilot</p>
          <h2>Ask your product what customers need next.</h2>
          <p>The copilot works over workspace-scoped feedback and operational context. It can summarize patterns, explain evidence, and prepare reviewable actions.</p>
          <div className={styles.promptList}><span>“What is driving negative sentiment this week?”</span><span>“Which urgent issues have no backlog action?”</span><span>“Prepare a ticket from the checkout feedback.”</span></div>
          <small>External AI execution is optional. Deterministic fallbacks keep the demo usable without a model key.</small>
        </div>
      </section>

      <section className={styles.architectureSection} id="architecture">
        <div className={styles.architectureHeading}>
          <p className={styles.eyebrowLight}>Built beyond the happy path</p>
          <h2>A portfolio project with production-shaped foundations.</h2>
          <p>PulsePilot demonstrates the full path from UI and secure APIs to asynchronous AI workflows, observability, and delivery automation.</p>
        </div>
        <div className={styles.architectureMap}>
          <div><small>Experience</small><strong>Next.js</strong><span>App Router · BFF · secure session</span></div><b>→</b>
          <div><small>Application</small><strong>.NET API</strong><span>JWT · validation · ProblemDetails</span></div><b>→</b>
          <div><small>Intelligence</small><strong>AI Worker</strong><span>Queue · tools · human approval</span></div><b>→</b>
          <div><small>Data</small><strong>PostgreSQL</strong><span>Workspace isolation · audit trail</span></div>
        </div>
        <div className={styles.proofGrid}>{proofPoints.map(([title, copy]) => <article key={title}><CheckIcon /><div><h3>{title}</h3><p>{copy}</p></div></article>)}</div>
        <div className={styles.stackRail}><span>.NET 10</span><span>Next.js 16</span><span>PostgreSQL</span><span>OpenAI-ready</span><span>OpenTelemetry</span><span>Docker</span><span>GitHub Actions</span></div>
      </section>

      <section className={styles.truthSection}>
        <div><p className={styles.eyebrow}>Honest by default</p><h2>Demo-ready today. Integration-ready by design.</h2></div>
        <p>The current release uses synthetic, deterministic product feedback and an ingestion-ready API. Native Zendesk, Intercom, app-store, and survey connectors are roadmap work—not simulated production claims.</p>
      </section>

      <section className={styles.finalCta}>
        <span className={styles.ctaPulse} aria-hidden="true" />
        <p className={styles.eyebrowLight}>Turn feedback into forward motion</p>
        <h2>Give every customer signal a path to action.</h2>
        <p>Explore the deterministic demo workspace and follow the evidence from dashboard signal to human-reviewed engineering action.</p>
        <div className={styles.heroActions}>
          <Link className={styles.primaryButton} href={primaryHref}>{primaryLabel}<ArrowIcon /></Link>
          <a className={styles.darkSecondary} href="https://github.com/mehmetanil10/pulsepilot-ai" target="_blank" rel="noreferrer">View source on GitHub</a>
        </div>
      </section>

      <footer className={styles.footer}>
        <PulseMark /><p>AI-driven product feedback &amp; engineering copilot.</p>
        <div><a href="#product">Product</a><a href="#architecture">Architecture</a><Link href={primaryHref}>{authenticated ? "Dashboard" : "Sign in"}</Link></div>
      </footer>
    </main>
  );
}
