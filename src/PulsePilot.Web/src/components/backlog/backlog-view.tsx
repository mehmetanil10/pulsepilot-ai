import Link from "next/link";

import { Icon } from "@/components/icons";
import { backlogHref } from "@/lib/backlog/query";
import type {
  BacklogFilters,
  BacklogItem,
  BacklogItemPriority,
  BacklogItemStatus,
  BacklogListPage,
} from "@/types/backlog";

const lifecycle: Array<{
  value: BacklogItemStatus;
  label: string;
  description: string;
}> = [
  { value: "open", label: "Open", description: "Ready for planning" },
  { value: "inProgress", label: "In progress", description: "Engineering active" },
  { value: "resolved", label: "Resolved", description: "Solution delivered" },
  { value: "closed", label: "Closed", description: "Outcome verified" },
];

export function BacklogView({
  filters,
  page,
}: {
  filters: BacklogFilters;
  page: BacklogListPage;
}) {
  const totalPages = Math.max(1, Math.ceil(page.totalCount / page.pageSize));
  const activeFilters = Number(filters.status !== "all")
    + Number(filters.priority !== "all")
    + Number(Boolean(filters.sourcePendingActionId));

  return (
    <main className="backlog-page">
      <header className="backlog-page-header">
        <div>
          <p className="eyebrow">Product-to-engineering trace</p>
          <h1>Engineering backlog</h1>
          <p>Follow approved customer signals from recommendation to engineering outcome.</p>
        </div>
        <div className="backlog-count">
          <strong>{formatNumber(page.totalCount)}</strong>
          <span>{activeFilters ? "matching work items" : "workspace work items"}</span>
        </div>
      </header>

      <section className="backlog-lifecycle" aria-label="Backlog lifecycle">
        {lifecycle.map((step, index) => (
          <div
            className={filters.status === step.value ? "active" : undefined}
            key={step.value}
          >
            <span>{String(index + 1).padStart(2, "0")}</span>
            <p><strong>{step.label}</strong><small>{step.description}</small></p>
            {index < lifecycle.length - 1 && <Icon name="arrow" />}
          </div>
        ))}
      </section>

      {filters.sourcePendingActionId && (
        <section className="backlog-source-trace" aria-label="Source action filter">
          <span><Icon name="actions" /></span>
          <div>
            <strong>Showing work created by one approved action</strong>
            <small>Source action {shortId(filters.sourcePendingActionId)}</small>
          </div>
          <Link href={backlogHref(filters, { sourcePendingActionId: undefined, page: 1 })}>
            Clear trace
          </Link>
        </section>
      )}

      <form className="backlog-filter-panel" action="/backlog" method="get">
        {filters.sourcePendingActionId && (
          <input
            name="sourcePendingActionId"
            type="hidden"
            value={filters.sourcePendingActionId}
          />
        )}
        <label>
          <span>Status</span>
          <select defaultValue={filters.status} name="status">
            <option value="all">All statuses</option>
            <option value="open">Open</option>
            <option value="inProgress">In progress</option>
            <option value="resolved">Resolved</option>
            <option value="closed">Closed</option>
          </select>
        </label>
        <label>
          <span>Priority</span>
          <select defaultValue={filters.priority} name="priority">
            <option value="all">All priorities</option>
            <option value="p1">P1 · Critical</option>
            <option value="p2">P2 · High</option>
            <option value="p3">P3 · Medium</option>
            <option value="p4">P4 · Low</option>
          </select>
        </label>
        <div className="backlog-filter-actions">
          <small>{activeFilters} active {activeFilters === 1 ? "filter" : "filters"}</small>
          {activeFilters > 0 && <Link href="/backlog">Reset</Link>}
          <button type="submit">Apply filters</button>
        </div>
      </form>

      {page.items.length === 0 ? (
        <BacklogEmptyState filtered={activeFilters > 0} />
      ) : (
        <section className="backlog-grid" aria-label="Engineering work items">
          {page.items.map((item) => <BacklogCard item={item} key={item.id} />)}
        </section>
      )}

      {totalPages > 1 && (
        <nav className="feedback-pagination backlog-pagination" aria-label="Backlog pages">
          <PaginationLink
            disabled={page.page === 1}
            href={backlogHref(filters, { page: page.page - 1 })}
            label="Previous"
          />
          <div><span aria-current="page">{page.page} / {totalPages}</span></div>
          <PaginationLink
            disabled={page.page === totalPages}
            href={backlogHref(filters, { page: page.page + 1 })}
            label="Next"
          />
        </nav>
      )}
    </main>
  );
}

function BacklogCard({ item }: { item: BacklogItem }) {
  return (
    <article className={`backlog-card ${item.status}`}>
      <header>
        <span className={`priority-chip ${item.priority}`}>{priorityLabel(item.priority)}</span>
        <span className={`backlog-status-chip ${item.status}`}>
          <i />{statusLabel(item.status)}
        </span>
      </header>
      <div className="backlog-card-copy">
        <p className="eyebrow">Work item {shortId(item.id)}</p>
        <h2>{stripPriority(item.title)}</h2>
        <p>{item.description}</p>
      </div>
      <dl className="backlog-card-metadata">
        <div><dt>Source cluster</dt><dd>{shortId(item.sourceClusterId)}</dd></div>
        <div><dt>Approved action</dt><dd>{shortId(item.sourcePendingActionId)}</dd></div>
        <div><dt>Created by</dt><dd>{shortId(item.createdByUserId)}</dd></div>
      </dl>
      <footer>
        <span><Icon name="clock" />Created {formatDateTime(item.createdAt)}</span>
        <small>{updatedLabel(item)}</small>
      </footer>
    </article>
  );
}

function BacklogEmptyState({ filtered }: { filtered: boolean }) {
  return (
    <section className="backlog-empty-state">
      <span><Icon name="backlog" /></span>
      <h2>{filtered ? "No work matches these filters." : "The engineering backlog is clear."}</h2>
      <p>{filtered
        ? "Reset the filters to inspect the complete workspace backlog."
        : "When an admin approves an engineering recommendation, its traceable work item will appear here."}</p>
      {filtered ? <Link href="/backlog">Reset filters</Link> : <Link href="/actions">Review AI actions</Link>}
    </section>
  );
}

function PaginationLink({ href, disabled, label }: { href: string; disabled: boolean; label: string }) {
  return disabled ? <span aria-disabled="true">{label}</span> : <Link href={href}>{label}</Link>;
}

function statusLabel(status: BacklogItemStatus): string {
  return status === "inProgress" ? "In progress" : `${status[0].toUpperCase()}${status.slice(1)}`;
}

function priorityLabel(priority: BacklogItemPriority): string {
  const impact = { p1: "Critical", p2: "High", p3: "Medium", p4: "Low" }[priority];
  return `${priority.toUpperCase()} · ${impact}`;
}

function stripPriority(value: string): string {
  return value.replace(/^\[p[1-4]\]\s*/i, "");
}

function shortId(value: string): string {
  return `${value.slice(0, 8)}…`;
}

function updatedLabel(item: BacklogItem): string {
  return item.updatedAt === item.createdAt
    ? "No lifecycle changes yet"
    : `Updated ${formatDateTime(item.updatedAt)}`;
}

function formatDateTime(value: string): string {
  return new Intl.DateTimeFormat("en", {
    month: "short",
    day: "numeric",
    year: "numeric",
    timeZone: "UTC",
  }).format(new Date(value));
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat("en").format(value);
}
