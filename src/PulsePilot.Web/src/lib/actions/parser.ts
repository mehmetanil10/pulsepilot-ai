import type {
  PendingActionContext,
  PendingActionItem,
  PendingActionListPage,
} from "@/types/actions";

const actionTypes = new Set([
  "createEngineeringIssue",
  "draftCustomerResponse",
  "generateReport",
  "escalateIssue",
]);
const actionStatuses = new Set(["pending", "approved", "rejected", "executed", "failed"]);
const priorities = new Set(["p1", "p2", "p3", "p4"]);

export function parsePendingActionList(value: unknown): PendingActionListPage | null {
  if (!isRecord(value)
    || !isPositiveInteger(value.page)
    || !isPositiveInteger(value.pageSize)
    || !isNonNegativeInteger(value.totalCount)
    || !Array.isArray(value.items)
    || value.items.length > 100) return null;

  const items = value.items.map(parsePendingActionItem);
  if (items.some((item) => item === null)) return null;

  return {
    items: items as PendingActionItem[],
    page: value.page,
    pageSize: value.pageSize,
    totalCount: value.totalCount,
  };
}

export function parsePendingActionItem(value: unknown): PendingActionItem | null {
  if (!isRecord(value)
    || !isString(value.id, 100)
    || !isString(value.feedbackId, 100)
    || !isString(value.feedbackClusterId, 100)
    || !isKnownString(value.actionType, actionTypes)
    || !isString(value.title, 200)
    || !isString(value.description, 2_000)
    || !isKnownString(value.status, actionStatuses)
    || !isNullableIsoDate(value.approvedAt)
    || !isNullableIsoDate(value.rejectedAt)
    || !isNullableIsoDate(value.executedAt)
    || !isIsoDate(value.createdAt)
    || !isIsoDate(value.updatedAt)) return null;

  return {
    id: value.id,
    feedbackId: value.feedbackId,
    feedbackClusterId: value.feedbackClusterId,
    actionType: value.actionType,
    title: value.title,
    description: value.description,
    status: value.status,
    approvedAt: value.approvedAt,
    rejectedAt: value.rejectedAt,
    executedAt: value.executedAt,
    createdAt: value.createdAt,
    updatedAt: value.updatedAt,
    context: parseContext(value.payload),
  };
}

function parseContext(value: unknown): PendingActionContext {
  const input = isRecord(value) ? value : {};

  return {
    priority: isKnownString(input.priority, priorities) ? input.priority : null,
    priorityScore: isNumberBetween(input.priorityScore, 0, 100)
      ? input.priorityScore
      : null,
    category: isString(input.category, 50) ? input.category : null,
    component: isString(input.component, 50) ? input.component : null,
    feedbackCount: isPositiveInteger(input.feedbackCount) ? input.feedbackCount : null,
    suggestedAction: isString(input.suggestedAction, 4_000)
      ? input.suggestedAction
      : null,
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

function isString(value: unknown, maximumLength: number): value is string {
  return typeof value === "string" && value.length <= maximumLength;
}

function isKnownString(value: unknown, supported: ReadonlySet<string>): value is string {
  return isString(value, 50) && supported.has(value);
}

function isNonNegativeInteger(value: unknown): value is number {
  return typeof value === "number" && Number.isInteger(value) && value >= 0;
}

function isPositiveInteger(value: unknown): value is number {
  return isNonNegativeInteger(value) && value > 0;
}

function isNumberBetween(value: unknown, minimum: number, maximum: number): value is number {
  return typeof value === "number"
    && Number.isFinite(value)
    && value >= minimum
    && value <= maximum;
}

function isNullableIsoDate(value: unknown): value is string | null {
  return value === null || isIsoDate(value);
}

function isIsoDate(value: unknown): value is string {
  return isString(value, 100) && !Number.isNaN(Date.parse(value));
}
