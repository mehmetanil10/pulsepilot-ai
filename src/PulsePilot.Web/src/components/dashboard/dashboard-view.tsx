import Link from "next/link";

import { Icon } from "@/components/icons";
import type { BackendCurrentUserResponse } from "@/types/auth";
import type { DashboardData, DashboardTrendingIssue } from "@/types/dashboard";

type DashboardViewProps = {
  user: BackendCurrentUserResponse;
  data: DashboardData;
  periodDays: number;
};

const periods = [7, 30, 90];

export function DashboardView({ user, data, periodDays }: DashboardViewProps) {
  const { summary, trending } = data;
  const firstName = user.displayName.split(/\s+/)[0];
  const kpis = [
    { label: "Feedback today", value: summary.kpis.feedbackToday, note: "Since 00:00 UTC", icon: "feedback" as const, tone: "violet" },
    { label: "AI processed", value: summary.kpis.aiProcessed, note: `Last ${periodDays} days`, icon: "spark" as const, tone: "green" },
    { label: "Critical issues", value: summary.kpis.criticalIssues, note: "Active P1 clusters", icon: "alert" as const, tone: "red" },
    { label: "Pending actions", value: summary.kpis.pendingActions, note: "Awaiting review", icon: "clock" as const, tone: "amber" },
  ];
  const visibleCategories = summary.categories.filter((item) => item.count > 0);
  const maxCategoryCount = Math.max(1, ...visibleCategories.map((item) => item.count));

  return (
    <main className="dashboard-page">
      <header className="live-dashboard-header">
        <div>
          <p className="eyebrow">Signal overview</p>
          <h1>Good to see you, {firstName}.</h1>
          <p>Your workspace pulse, grounded in live product signals.</p>
        </div>
        <div className="dashboard-controls">
          <nav className="period-picker" aria-label="Dashboard period">
            {periods.map((period) => (
              <Link
                href={`/dashboard?periodDays=${period}`}
                aria-current={period === periodDays ? "page" : undefined}
                key={period}
              >
                {period}d
              </Link>
            ))}
          </nav>
          <span className="live-data-badge">
            <i /> Updated {relativeTime(summary.generatedAt, summary.generatedAt)}
          </span>
        </div>
      </header>

      <section className="live-kpi-grid" aria-label="Key performance indicators">
        {kpis.map((kpi) => (
          <article className={`live-kpi-card ${kpi.tone}`} key={kpi.label}>
            <div className="kpi-card-top">
              <span><Icon name={kpi.icon} /></span>
              <small>{kpi.note}</small>
            </div>
            <strong>{formatNumber(kpi.value)}</strong>
            <p>{kpi.label}</p>
          </article>
        ))}
      </section>

      <section className="dashboard-data-grid">
        <article className="dashboard-panel trending-panel">
          <PanelHeading eyebrow="Momentum" title="Trending issues" meta={`Last ${periodDays} days`} />
          {trending.items.length === 0 ? (
            <EmptyState icon="trend" text="No growing issue clusters in this period." />
          ) : (
            <ol className="trending-list">
              {trending.items.map((issue, index) => (
                <TrendingIssue issue={issue} rank={index + 1} key={issue.feedbackClusterId} />
              ))}
            </ol>
          )}
        </article>

        <article className="dashboard-panel health-panel">
          <PanelHeading eyebrow="Pipeline" title="AI processing health" meta={`${periodDays}d`} />
          <div className="health-score">
            <div className={summary.kpis.processingFailures > 0 ? "has-failures" : "is-healthy"}>
              <Icon name={summary.kpis.processingFailures > 0 ? "alert" : "spark"} />
            </div>
            <span>
              <strong>{formatNumber(summary.kpis.processingFailures)}</strong>
              <small>processing failures</small>
            </span>
          </div>
          <dl className="health-details">
            <div><dt>Analyzed</dt><dd>{formatNumber(summary.kpis.aiProcessed)}</dd></div>
            <div><dt>Average severity</dt><dd>{summary.kpis.averageSeverity?.toFixed(1) ?? "—"}<small>/5</small></dd></div>
            <div><dt>Window</dt><dd>{formatDate(summary.periodFromInclusive)} → now</dd></div>
          </dl>
          <p className="health-note">
            {summary.kpis.processingFailures > 0
              ? "Failed analyses remain visible and can be retried from feedback detail."
              : "All feedback processed in this window completed without failure."}
          </p>
        </article>

        <article className="dashboard-panel recent-panel">
          <PanelHeading eyebrow="Latest intake" title="Recent feedback" meta={`${summary.recentFeedback.length} shown`} />
          {summary.recentFeedback.length === 0 ? (
            <EmptyState icon="feedback" text="New feedback will appear here as it arrives." />
          ) : (
            <ul className="activity-list">
              {summary.recentFeedback.map((feedback) => (
                <li key={feedback.id}>
                  <span className={`status-dot ${feedback.processingStatus}`} />
                  <div>
                    <strong>{feedback.title || "Untitled feedback"}</strong>
                    <small>{displayName(feedback.source)} · {relativeTime(feedback.createdAt, summary.generatedAt)}</small>
                  </div>
                  <span className={`status-label ${feedback.processingStatus}`}>{displayName(feedback.processingStatus)}</span>
                </li>
              ))}
            </ul>
          )}
        </article>

        <article className="dashboard-panel actions-panel">
          <PanelHeading eyebrow="Human review" title="Pending AI actions" meta={`${summary.kpis.pendingActions} total`} />
          {summary.pendingActions.length === 0 ? (
            <EmptyState icon="actions" text="No AI recommendations are waiting for review." />
          ) : (
            <ul className="action-preview-list">
              {summary.pendingActions.map((action) => (
                <li key={action.id}>
                  <span><Icon name={action.actionType === "draftCustomerResponse" ? "feedback" : "backlog"} /></span>
                  <div>
                    <strong>{action.title}</strong>
                    <small>{displayName(action.actionType)} · {relativeTime(action.createdAt, summary.generatedAt)}</small>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </article>

        <article className="dashboard-panel category-panel">
          <PanelHeading eyebrow="Composition" title="Feedback categories" meta={`${periodDays}d analyzed`} />
          {visibleCategories.length === 0 ? (
            <EmptyState icon="dashboard" text="Category mix appears after AI analysis completes." />
          ) : (
            <div className="category-bars">
              {visibleCategories.map((item) => (
                <div key={item.category}>
                  <span>{displayName(item.category)}</span>
                  <i><b style={{ width: `${Math.max(5, item.count * 100 / maxCategoryCount)}%` }} /></i>
                  <strong>{item.count}</strong>
                </div>
              ))}
            </div>
          )}
        </article>
      </section>
    </main>
  );
}

function PanelHeading({ eyebrow, title, meta }: { eyebrow: string; title: string; meta: string }) {
  return (
    <header className="panel-heading">
      <div><p className="eyebrow">{eyebrow}</p><h2>{title}</h2></div>
      <span>{meta}</span>
    </header>
  );
}

function TrendingIssue({ issue, rank }: { issue: DashboardTrendingIssue; rank: number }) {
  const growth = issue.isNew ? "New" : `+${issue.growthPercentage?.toFixed(0) ?? 0}%`;
  return (
    <li>
      <span className="trend-rank">{String(rank).padStart(2, "0")}</span>
      <div className="trend-copy">
        <strong>{issue.title}</strong>
        <small>{displayName(issue.component)} · {displayName(issue.category)}</small>
      </div>
      <span className={`priority-chip ${issue.priority}`}>{issue.priority.toUpperCase()}</span>
      <div className="trend-count">
        <strong>{issue.currentPeriodCount}</strong>
        <small>{growth}</small>
      </div>
    </li>
  );
}

function EmptyState({ icon, text }: { icon: "trend" | "feedback" | "actions" | "dashboard"; text: string }) {
  return <div className="dashboard-empty"><Icon name={icon} /><p>{text}</p></div>;
}

function displayName(value: string): string {
  return value
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/^./, (letter) => letter.toUpperCase());
}

function relativeTime(value: string, reference: string): string {
  const minutes = Math.max(0, Math.floor((Date.parse(reference) - Date.parse(value)) / 60_000));
  if (minutes < 1) return "just now";
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  return `${Math.floor(hours / 24)}d ago`;
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat("en", { month: "short", day: "numeric", timeZone: "UTC" })
    .format(new Date(value));
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat("en", { notation: value > 9_999 ? "compact" : "standard" })
    .format(value);
}
