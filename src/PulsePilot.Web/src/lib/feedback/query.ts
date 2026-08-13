import {
  feedbackCategories,
  feedbackComponents,
  feedbackSentiments,
  feedbackSources,
  processingStatuses,
} from "./options";
import type { FeedbackListFilters } from "@/types/feedback";

export const feedbackPageSize = 12;

type RawSearchParams = Record<string, string | string[] | undefined>;

const values = {
  source: new Set(feedbackSources.map(([value]) => value)),
  processingStatus: new Set(processingStatuses.map(([value]) => value)),
  category: new Set(feedbackCategories.map(([value]) => value)),
  component: new Set(feedbackComponents.map(([value]) => value)),
  sentiment: new Set(feedbackSentiments.map(([value]) => value)),
};

export function parseFeedbackFilters(params: RawSearchParams): FeedbackListFilters {
  const page = parsePositiveInteger(single(params.page)) ?? 1;
  const severity = parsePositiveInteger(single(params.severity));
  const dateFrom = parseIsoDate(single(params.dateFrom));
  const dateTo = parseIsoDate(single(params.dateTo));
  const validRange = !dateFrom || !dateTo || dateFrom <= dateTo;
  const search = single(params.search)?.trim().slice(0, 200);

  return compact({
    page,
    source: allowed(single(params.source), values.source),
    processingStatus: allowed(single(params.processingStatus), values.processingStatus),
    category: allowed(single(params.category), values.category),
    component: allowed(single(params.component), values.component),
    severity: severity && severity <= 5 ? severity : undefined,
    sentiment: allowed(single(params.sentiment), values.sentiment),
    dateFrom: validRange ? dateFrom : undefined,
    dateTo: validRange ? dateTo : undefined,
    search: search || undefined,
  });
}

export function feedbackHref(
  filters: FeedbackListFilters,
  overrides: Partial<FeedbackListFilters> = {},
): string {
  const next = compact({ ...filters, ...overrides });
  const params = new URLSearchParams();

  for (const [key, value] of Object.entries(next)) {
    if (key === "page" && value === 1) continue;
    params.set(key, String(value));
  }

  const query = params.toString();
  return query ? `/feedback?${query}` : "/feedback";
}

export function countActiveFeedbackFilters(filters: FeedbackListFilters): number {
  return Object.entries(filters)
    .filter(([key, value]) => key !== "page" && value !== undefined)
    .length;
}

function single(value: string | string[] | undefined): string | undefined {
  return typeof value === "string" ? value : undefined;
}

function allowed(value: string | undefined, allowedValues: ReadonlySet<string>) {
  return value && allowedValues.has(value) ? value : undefined;
}

function parsePositiveInteger(value: string | undefined): number | undefined {
  if (!value || !/^\d+$/.test(value)) return undefined;
  const number = Number(value);
  return Number.isSafeInteger(number) && number > 0 ? number : undefined;
}

function parseIsoDate(value: string | undefined): string | undefined {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return undefined;
  const date = new Date(`${value}T00:00:00Z`);
  return !Number.isNaN(date.getTime()) && date.toISOString().slice(0, 10) === value
    ? value
    : undefined;
}

function compact<T extends Record<string, unknown>>(value: T): T {
  return Object.fromEntries(
    Object.entries(value).filter(([, item]) => item !== undefined),
  ) as T;
}
