import type { FeedbackListItem, FeedbackListPage } from "@/types/feedback";

export function parseFeedbackListPage(value: unknown): FeedbackListPage | null {
  if (!isRecord(value)
    || !isPositiveInteger(value.page)
    || !isPositiveInteger(value.pageSize)
    || !isNonNegativeInteger(value.totalCount)
    || !Array.isArray(value.items)
    || value.items.length > 100) return null;

  const items = value.items.map(parseFeedbackListItem);
  if (items.some((item) => item === null)) return null;

  return {
    items: items as FeedbackListItem[],
    page: value.page,
    pageSize: value.pageSize,
    totalCount: value.totalCount,
  };
}

function parseFeedbackListItem(value: unknown): FeedbackListItem | null {
  if (!isRecord(value)
    || !isString(value.id, 100)
    || !(value.feedbackClusterId === null || isString(value.feedbackClusterId, 100))
    || !(value.title === null || isString(value.title, 500))
    || !isString(value.content, 20_000)
    || !isString(value.source, 50)
    || !isString(value.processingStatus, 50)
    || !isIsoDate(value.createdAt)
    || !isIsoDate(value.updatedAt)
    || !(value.category === null || isString(value.category, 50))
    || !(value.component === null || isString(value.component, 50))
    || !(value.severity === null || isSeverity(value.severity))
    || !(value.sentiment === null || isString(value.sentiment, 50))) return null;

  return {
    id: value.id,
    feedbackClusterId: value.feedbackClusterId,
    title: value.title,
    content: value.content,
    source: value.source,
    processingStatus: value.processingStatus,
    createdAt: value.createdAt,
    updatedAt: value.updatedAt,
    category: value.category,
    component: value.component,
    severity: value.severity,
    sentiment: value.sentiment,
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

function isString(value: unknown, maximumLength: number): value is string {
  return typeof value === "string" && value.length <= maximumLength;
}

function isNonNegativeInteger(value: unknown): value is number {
  return typeof value === "number" && Number.isInteger(value) && value >= 0;
}

function isPositiveInteger(value: unknown): value is number {
  return isNonNegativeInteger(value) && value > 0;
}

function isSeverity(value: unknown): value is number {
  return isPositiveInteger(value) && value <= 5;
}

function isIsoDate(value: unknown): value is string {
  return isString(value, 100) && !Number.isNaN(Date.parse(value));
}
