import Link from "next/link";

import { Icon } from "@/components/icons";
import {
  feedbackCategories,
  feedbackComponents,
  feedbackSentiments,
  feedbackSources,
  processingStatuses,
} from "@/lib/feedback/options";
import {
  countActiveFeedbackFilters,
  feedbackHref,
} from "@/lib/feedback/query";
import {
  displayFeedbackValue,
  formatFeedbackDateTime,
} from "@/lib/feedback/presentation";
import type {
  FeedbackListFilters,
  FeedbackListItem,
  FeedbackListPage,
} from "@/types/feedback";

type FeedbackListViewProps = {
  filters: FeedbackListFilters;
  page: FeedbackListPage;
};

export function FeedbackListView({ filters, page }: FeedbackListViewProps) {
  const activeFilterCount = countActiveFeedbackFilters(filters);
  const totalPages = Math.max(1, Math.ceil(page.totalCount / page.pageSize));
  const firstResult = page.totalCount === 0 ? 0 : (page.page - 1) * page.pageSize + 1;
  const lastResult = Math.min(page.page * page.pageSize, page.totalCount);

  return (
    <main className="feedback-page">
      <header className="feedback-page-header">
        <div>
          <p className="eyebrow">Customer signal library</p>
          <h1>Feedback intelligence</h1>
          <p>Search every product signal and inspect its AI processing context.</p>
        </div>
        <div className="feedback-count">
          <strong>{formatNumber(page.totalCount)}</strong>
          <span>workspace signals</span>
        </div>
      </header>

      <form className="feedback-filter-panel" action="/feedback" method="get">
        <div className="feedback-search-field">
          <Icon name="feedback" />
          <label htmlFor="feedback-search">Search feedback</label>
          <input
            id="feedback-search"
            name="search"
            defaultValue={filters.search}
            maxLength={200}
            placeholder="Search title or original feedback…"
          />
        </div>
        <div className="feedback-filter-grid">
          <FilterSelect label="Source" name="source" value={filters.source} options={feedbackSources} />
          <FilterSelect label="Status" name="processingStatus" value={filters.processingStatus} options={processingStatuses} />
          <FilterSelect label="Category" name="category" value={filters.category} options={feedbackCategories} />
          <FilterSelect label="Component" name="component" value={filters.component} options={feedbackComponents} />
          <FilterSelect
            label="Severity"
            name="severity"
            value={filters.severity?.toString()}
            options={[["5", "5 · Critical"], ["4", "4 · High"], ["3", "3 · Medium"], ["2", "2 · Low"], ["1", "1 · Minimal"]]}
          />
          <FilterSelect label="Sentiment" name="sentiment" value={filters.sentiment} options={feedbackSentiments} />
          <label className="filter-control">
            <span>From</span>
            <input type="date" name="dateFrom" defaultValue={filters.dateFrom} />
          </label>
          <label className="filter-control">
            <span>To</span>
            <input type="date" name="dateTo" defaultValue={filters.dateTo} />
          </label>
        </div>
        <div className="filter-actions">
          <span>{activeFilterCount === 0 ? "All feedback" : `${activeFilterCount} active filter${activeFilterCount === 1 ? "" : "s"}`}</span>
          <div>
            {activeFilterCount > 0 && <Link href="/feedback">Clear all</Link>}
            <button type="submit">Apply filters <Icon name="arrow" /></button>
          </div>
        </div>
      </form>

      <section className="feedback-list-panel" aria-labelledby="feedback-results-heading">
        <header>
          <div>
            <p className="eyebrow">Signal stream</p>
            <h2 id="feedback-results-heading">Feedback results</h2>
          </div>
          <span>{firstResult}–{lastResult} of {formatNumber(page.totalCount)}</span>
        </header>

        {page.items.length === 0 ? (
          <div className="feedback-empty-state">
            <span><Icon name="feedback" /></span>
            <h3>{activeFilterCount ? "No signals match these filters." : "No feedback has arrived yet."}</h3>
            <p>{activeFilterCount ? "Try widening the date range or removing a filter." : "New customer signals will appear here as they are collected."}</p>
            {activeFilterCount > 0 && <Link href="/feedback">View all feedback</Link>}
          </div>
        ) : (
          <>
            <div className="feedback-table-heading" aria-hidden="true">
              <span>Signal</span><span>AI analysis</span><span>Severity</span><span>Source</span><span>Status</span>
            </div>
            <div className="feedback-results-list">
              {page.items.map((item) => <FeedbackRow item={item} key={item.id} />)}
            </div>
          </>
        )}
      </section>

      {totalPages > 1 && (
        <nav className="feedback-pagination" aria-label="Feedback pages">
          <PaginationLink
            href={feedbackHref(filters, { page: page.page - 1 })}
            disabled={page.page === 1}
            label="Previous"
          />
          <div>
            {paginationWindow(page.page, totalPages).map((value, index) => value === "…" ? (
              <span className="pagination-gap" key={`gap-${index}`}>…</span>
            ) : (
              <Link
                href={feedbackHref(filters, { page: value })}
                aria-current={value === page.page ? "page" : undefined}
                key={value}
              >
                {value}
              </Link>
            ))}
          </div>
          <PaginationLink
            href={feedbackHref(filters, { page: page.page + 1 })}
            disabled={page.page === totalPages}
            label="Next"
          />
        </nav>
      )}
    </main>
  );
}

function FilterSelect({
  label,
  name,
  value,
  options,
}: {
  label: string;
  name: string;
  value?: string;
  options: ReadonlyArray<readonly [string, string]>;
}) {
  return (
    <label className="filter-control">
      <span>{label}</span>
      <select name={name} defaultValue={value ?? ""}>
        <option value="">All</option>
        {options.map(([optionValue, optionLabel]) => (
          <option value={optionValue} key={optionValue}>{optionLabel}</option>
        ))}
      </select>
    </label>
  );
}

function FeedbackRow({ item }: { item: FeedbackListItem }) {
  return (
    <Link className="feedback-row" href={`/feedback/${item.id}`} prefetch={false}>
      <div className="feedback-signal-copy">
        <strong>{item.title || "Untitled feedback"}</strong>
        <p>{item.content}</p>
        <small>{formatFeedbackDateTime(item.createdAt)}</small>
      </div>
      <div className="feedback-analysis-cell">
        {item.category ? (
          <><strong>{displayFeedbackValue(item.category)}</strong><small>{displayFeedbackValue(item.component ?? "general")} · {displayFeedbackValue(item.sentiment ?? "neutral")}</small></>
        ) : (
          <><strong>Awaiting analysis</strong><small>AI metadata pending</small></>
        )}
      </div>
      <div className="severity-cell">
        {item.severity ? (
          <><span>{item.severity}/5</span><i>{[1, 2, 3, 4, 5].map((level) => <b className={level <= item.severity! ? "filled" : undefined} key={level} />)}</i></>
        ) : <span className="not-available">—</span>}
      </div>
      <span className="source-chip">{displayFeedbackValue(item.source)}</span>
      <span className={`feedback-status-chip ${item.processingStatus}`}>{displayFeedbackValue(item.processingStatus)}</span>
      <Icon name="arrow" />
    </Link>
  );
}

function PaginationLink({ href, disabled, label }: { href: string; disabled: boolean; label: string }) {
  return disabled ? <span aria-disabled="true">{label}</span> : <Link href={href}>{label}</Link>;
}

function paginationWindow(current: number, total: number): Array<number | "…"> {
  if (total <= 7) return Array.from({ length: total }, (_, index) => index + 1);
  if (current <= 4) return [1, 2, 3, 4, 5, "…", total];
  if (current >= total - 3) return [1, "…", total - 4, total - 3, total - 2, total - 1, total];
  return [1, "…", current - 1, current, current + 1, "…", total];
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat("en").format(value);
}
