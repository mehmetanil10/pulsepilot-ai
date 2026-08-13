import Link from "next/link";

import { ActionReviewControls } from "@/components/actions/action-review-controls";
import { Icon } from "@/components/icons";
import { pendingActionHref } from "@/lib/actions/query";
import type {
  ActionStatusFilter,
  PendingActionFilters,
  PendingActionItem,
  PendingActionListPage,
} from "@/types/actions";

const statusTabs: Array<{ value: ActionStatusFilter; label: string }> = [
  { value: "pending", label: "Pending" },
  { value: "approved", label: "Approved" },
  { value: "executed", label: "Executed" },
  { value: "rejected", label: "Rejected" },
  { value: "failed", label: "Failed" },
  { value: "all", label: "All" },
];

export function PendingActionsView({
  filters,
  page,
  canReview,
}: {
  filters: PendingActionFilters;
  page: PendingActionListPage;
  canReview: boolean;
}) {
  const totalPages = Math.max(1, Math.ceil(page.totalCount / page.pageSize));

  return (
    <main className="actions-page">
      <header className="actions-page-header">
        <div>
          <p className="eyebrow">Human-in-the-loop</p>
          <h1>Review AI actions</h1>
          <p>Inspect the evidence, then decide what PulsePilot is allowed to execute.</p>
        </div>
        <div className="actions-count">
          <strong>{formatNumber(page.totalCount)}</strong>
          <span>{filters.status === "all" ? "total recommendations" : `${filters.status} recommendations`}</span>
        </div>
      </header>

      <section className="review-safety-strip" aria-label="Action review workflow">
        <div><span>01</span><p><strong>AI recommends</strong><small>Evidence and context are prepared.</small></p></div>
        <Icon name="arrow" />
        <div><span>02</span><p><strong>Human decides</strong><small>An admin approves or rejects.</small></p></div>
        <Icon name="arrow" />
        <div><span>03</span><p><strong>Backend executes</strong><small>Only allowlisted tools can run.</small></p></div>
      </section>

      <nav className="action-status-tabs" aria-label="Action status">
        {statusTabs.map((tab) => (
          <Link
            href={pendingActionHref(filters, { status: tab.value, page: 1 })}
            aria-current={filters.status === tab.value ? "page" : undefined}
            key={tab.value}
          >
            {tab.label}
          </Link>
        ))}
      </nav>

      {page.items.length === 0 ? (
        <ActionsEmptyState status={filters.status} />
      ) : (
        <section className="pending-action-list" aria-label="AI action recommendations">
          {page.items.map((action) => (
            <PendingActionCard action={action} canReview={canReview} key={action.id} />
          ))}
        </section>
      )}

      {totalPages > 1 && (
        <nav className="feedback-pagination actions-pagination" aria-label="Action pages">
          <PaginationLink href={pendingActionHref(filters, { page: page.page - 1 })} disabled={page.page === 1} label="Previous" />
          <div><span aria-current="page">{page.page} / {totalPages}</span></div>
          <PaginationLink href={pendingActionHref(filters, { page: page.page + 1 })} disabled={page.page === totalPages} label="Next" />
        </nav>
      )}
    </main>
  );
}

function PendingActionCard({ action, canReview }: { action: PendingActionItem; canReview: boolean }) {
  const priority = actionPriority(action);
  const clusterTitle = action.title.replace(/^\[[^\]]+\]\s*/, "");
  const decisionAt = action.executedAt ?? action.rejectedAt ?? action.approvedAt;

  return (
    <article className={`pending-action-card ${action.status}`}>
      <header className="pending-action-heading">
        <span className={`action-type-icon ${action.actionType}`}><Icon name={actionIcon(action.actionType)} /></span>
        <div>
          <p>{displayName(action.actionType)}</p>
          <h2>{clusterTitle}</h2>
        </div>
        {priority && <span className={`priority-chip ${priority.toLowerCase()}`}>{priority.toUpperCase()}</span>}
        <span className={`action-status-chip ${action.status}`}>{displayName(action.status)}</span>
      </header>

      <div className="pending-action-body">
        <div className="action-recommendation-copy">
          <section>
            <p className="eyebrow">Recommended action</p>
            <h3>{action.description}</h3>
            <p>{actionEffect(action.actionType)}</p>
          </section>
          <section className="action-reason">
            <span><Icon name="spark" /></span>
            <div>
              <small>Why PulsePilot recommended this</small>
              <p>{recommendationReason(action)}</p>
            </div>
          </section>
        </div>

        <aside className="action-context-panel" aria-label="Recommendation context">
          <div className="action-priority-score">
            <span>Priority</span>
            <strong>{priority?.toUpperCase() ?? "—"}</strong>
            <small>{action.context.priorityScore === null ? "Score unavailable" : `${action.context.priorityScore.toFixed(1)} / 100`}</small>
          </div>
          <dl>
            <div><dt>Cluster volume</dt><dd>{action.context.feedbackCount === null ? "—" : formatNumber(action.context.feedbackCount)}</dd></div>
            <div><dt>Category</dt><dd>{action.context.category ? displayName(action.context.category) : "—"}</dd></div>
            <div><dt>Component</dt><dd>{action.context.component ? displayName(action.context.component) : "—"}</dd></div>
            <div><dt>Created</dt><dd>{formatDateTime(action.createdAt)}</dd></div>
          </dl>
          <div className="action-related-links">
            <Link href={`/feedback/${action.feedbackId}`} prefetch={false}>
              <Icon name="feedback" /><span><strong>Related feedback</strong><small>Open source signal</small></span><Icon name="arrow" />
            </Link>
            <div>
              <Icon name="trend" /><span><strong>Related cluster</strong><small>{shortId(action.feedbackClusterId)} · {clusterTitle}</small></span>
            </div>
          </div>
        </aside>
      </div>

      <footer className="pending-action-footer">
        {action.status === "pending" ? (
          <ActionReviewControls actionId={action.id} actionType={action.actionType} canReview={canReview} />
        ) : (
          <div className="action-decision-summary">
            <Icon name={action.status === "rejected" || action.status === "failed" ? "alert" : "actions"} />
            <span>
              <strong>{decisionSummary(action.status)}</strong>
              <small>{decisionAt ? formatDateTime(decisionAt) : `Last updated ${formatDateTime(action.updatedAt)}`}</small>
            </span>
          </div>
        )}
      </footer>
    </article>
  );
}

function ActionsEmptyState({ status }: { status: ActionStatusFilter }) {
  return (
    <section className="actions-empty-state">
      <span><Icon name={status === "pending" ? "actions" : "clock"} /></span>
      <h2>{status === "pending" ? "The review queue is clear." : `No ${status} recommendations found.`}</h2>
      <p>{status === "pending"
        ? "New high-priority recommendations will appear here before anything is allowed to run."
        : "Choose another status to inspect the action history."}</p>
      {status !== "pending" && <Link href="/actions">View pending actions</Link>}
    </section>
  );
}

function PaginationLink({ href, disabled, label }: { href: string; disabled: boolean; label: string }) {
  return disabled ? <span aria-disabled="true">{label}</span> : <Link href={href}>{label}</Link>;
}

function actionPriority(action: PendingActionItem): string | null {
  if (action.context.priority) return action.context.priority;
  return action.title.match(/^\[(p[1-4])\]/i)?.[1] ?? null;
}

function recommendationReason(action: PendingActionItem): string {
  if (action.context.suggestedAction) return action.context.suggestedAction;
  if (action.context.feedbackCount && actionPriority(action)) {
    return `This ${actionPriority(action)!.toUpperCase()} cluster contains ${action.context.feedbackCount} related customer signals and crossed the deterministic action threshold.`;
  }
  return "The deterministic recommendation policy identified this cluster for human review.";
}

function actionEffect(actionType: string): string {
  switch (actionType) {
    case "createEngineeringIssue": return "Approval creates one workspace backlog item; duplicate active issues are blocked.";
    case "draftCustomerResponse": return "Approval generates an unsent draft. PulsePilot never sends the response automatically.";
    case "generateReport": return "Approval generates a bounded workspace report through the controlled backend tool.";
    default: return "Approval records the decision without granting the model unrestricted execution access.";
  }
}

function decisionSummary(status: string): string {
  switch (status) {
    case "executed": return "Approved action executed";
    case "approved": return "Approved and awaiting execution";
    case "rejected": return "Recommendation rejected";
    case "failed": return "Approved action did not complete";
    default: return displayName(status);
  }
}

function actionIcon(actionType: string): "alert" | "backlog" | "dashboard" | "feedback" {
  switch (actionType) {
    case "createEngineeringIssue": return "backlog";
    case "draftCustomerResponse": return "feedback";
    case "generateReport": return "dashboard";
    default: return "alert";
  }
}

function displayName(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, "$1 $2").replace(/^./, (letter) => letter.toUpperCase());
}

function shortId(value: string): string {
  return `${value.slice(0, 8)}…`;
}

function formatDateTime(value: string): string {
  return new Intl.DateTimeFormat("en", {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "UTC",
    timeZoneName: "short",
  }).format(new Date(value));
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat("en").format(value);
}
