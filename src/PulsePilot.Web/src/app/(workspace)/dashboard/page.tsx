import type { Metadata } from "next";

import { Icon } from "@/components/icons";
import { requireUser } from "@/lib/auth/session";

export const metadata: Metadata = { title: "Dashboard" };

const capabilities = [
  {
    label: "Feedback signals",
    copy: "Workspace-scoped feedback and analysis",
    icon: "feedback" as const,
    tone: "violet",
  },
  {
    label: "Review queue",
    copy: "Human approval before every action",
    icon: "actions" as const,
    tone: "amber",
  },
  {
    label: "Engineering backlog",
    copy: "Traceable recommendations and priorities",
    icon: "backlog" as const,
    tone: "blue",
  },
  {
    label: "Product copilot",
    copy: "Bounded tools with workspace context",
    icon: "copilot" as const,
    tone: "green",
  },
];

export default async function DashboardPage() {
  const user = await requireUser();
  const firstName = user.displayName.split(/\s+/)[0];

  return (
    <main className="dashboard-page">
      <header className="dashboard-topbar">
        <div>
          <p className="eyebrow">Signal overview</p>
          <h1>Good to see you, {firstName}.</h1>
        </div>
        <span className="foundation-badge"><i /> Frontend foundation online</span>
      </header>

      <section className="dashboard-intro">
        <div>
          <span className="intro-icon"><Icon name="spark" /></span>
          <p>Workspace ready</p>
          <h2>Your product intelligence command center is connected.</h2>
          <span>
            Authentication, secure API access, navigation, and the deployment
            baseline are in place. Live dashboard data arrives in Task 28.
          </span>
        </div>
        <div className="orbit-visual" aria-hidden="true">
          <span className="orbit orbit-one" />
          <span className="orbit orbit-two" />
          <span className="orbit-core"><Icon name="spark" /></span>
          <i className="orbit-node node-one" />
          <i className="orbit-node node-two" />
          <i className="orbit-node node-three" />
        </div>
      </section>

      <section className="capability-section" aria-labelledby="capability-heading">
        <div className="section-heading">
          <div>
            <p className="eyebrow">Product loop</p>
            <h2 id="capability-heading">One workspace, every decision</h2>
          </div>
          <span>Task 27 foundation</span>
        </div>
        <div className="capability-grid">
          {capabilities.map((capability, index) => (
            <article className={`capability-card ${capability.tone}`} key={capability.label}>
              <div className="capability-card-head">
                <span><Icon name={capability.icon} /></span>
                <small>{String(index + 1).padStart(2, "0")}</small>
              </div>
              <h3>{capability.label}</h3>
              <p>{capability.copy}</p>
              <div className="capability-status"><i /> Connected</div>
            </article>
          ))}
        </div>
      </section>

      <section className="architecture-strip">
        <div>
          <p className="eyebrow">Secure by default</p>
          <h2>The token stays server-side.</h2>
          <span>
            Browser requests pass through the PulsePilot web gateway. The API
            remains the source of truth for authentication and workspace authorization.
          </span>
        </div>
        <ol aria-label="Request architecture">
          <li><strong>Browser</strong><span>Same-origin request</span></li>
          <li><strong>Next.js BFF</strong><span>HttpOnly session</span></li>
          <li><strong>PulsePilot API</strong><span>JWT + workspace checks</span></li>
        </ol>
      </section>
    </main>
  );
}
