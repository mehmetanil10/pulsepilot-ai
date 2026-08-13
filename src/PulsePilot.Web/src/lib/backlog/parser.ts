import type {
  BacklogItem,
  BacklogItemPriority,
  BacklogItemStatus,
  BacklogListPage,
} from "@/types/backlog";

const statuses = new Set<BacklogItemStatus>([
  "open",
  "inProgress",
  "resolved",
  "closed",
]);
const priorities = new Set<BacklogItemPriority>(["p1", "p2", "p3", "p4"]);
const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function parseBacklogList(value: unknown): BacklogListPage | null {
  if (!isRecord(value)
    || !isPositiveInteger(value.page)
    || !isPositiveInteger(value.pageSize)
    || !isNonNegativeInteger(value.totalCount)
    || !Array.isArray(value.items)
    || value.items.length > 100) return null;

  const items = value.items.map(parseBacklogItem);
  if (items.some((item) => item === null)) return null;

  return {
    items: items as BacklogItem[],
    page: value.page,
    pageSize: value.pageSize,
    totalCount: value.totalCount,
  };
}

export function parseBacklogItem(value: unknown): BacklogItem | null {
  if (!isRecord(value)
    || !isGuid(value.id)
    || !isGuid(value.sourceClusterId)
    || !isGuid(value.sourcePendingActionId)
    || !isGuid(value.createdByUserId)
    || !isRequiredString(value.title, 200)
    || !isRequiredString(value.description, 4_000)
    || !isKnownString(value.priority, priorities)
    || !isKnownString(value.status, statuses)
    || !isIsoDate(value.createdAt)
    || !isIsoDate(value.updatedAt)) return null;

  return {
    id: value.id,
    sourceClusterId: value.sourceClusterId,
    sourcePendingActionId: value.sourcePendingActionId,
    createdByUserId: value.createdByUserId,
    title: value.title,
    description: value.description,
    priority: value.priority,
    status: value.status,
    createdAt: value.createdAt,
    updatedAt: value.updatedAt,
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

function isRequiredString(value: unknown, maximumLength: number): value is string {
  return typeof value === "string"
    && value.trim().length > 0
    && value.length <= maximumLength;
}

function isKnownString<T extends string>(
  value: unknown,
  supported: ReadonlySet<T>,
): value is T {
  return typeof value === "string" && supported.has(value as T);
}

function isGuid(value: unknown): value is string {
  return typeof value === "string"
    && guidPattern.test(value)
    && value !== "00000000-0000-0000-0000-000000000000";
}

function isNonNegativeInteger(value: unknown): value is number {
  return typeof value === "number" && Number.isInteger(value) && value >= 0;
}

function isPositiveInteger(value: unknown): value is number {
  return isNonNegativeInteger(value) && value > 0;
}

function isIsoDate(value: unknown): value is string {
  return typeof value === "string"
    && value.length <= 100
    && !Number.isNaN(Date.parse(value));
}
