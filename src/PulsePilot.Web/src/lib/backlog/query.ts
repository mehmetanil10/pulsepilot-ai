import type {
  BacklogFilters,
  BacklogPriorityFilter,
  BacklogStatusFilter,
} from "@/types/backlog";

export const backlogPageSize = 12;

const statuses = new Set<BacklogStatusFilter>([
  "all",
  "open",
  "inProgress",
  "resolved",
  "closed",
]);
const priorities = new Set<BacklogPriorityFilter>(["all", "p1", "p2", "p3", "p4"]);
const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function parseBacklogFilters(
  input: Record<string, string | string[] | undefined>,
): BacklogFilters {
  const pageValue = single(input.page);
  const parsedPage = pageValue ? Number(pageValue) : 1;
  const page = Number.isSafeInteger(parsedPage) && parsedPage > 0 ? parsedPage : 1;
  const statusValue = single(input.status);
  const status = statusValue && statuses.has(statusValue as BacklogStatusFilter)
    ? statusValue as BacklogStatusFilter
    : "all";
  const priorityValue = single(input.priority);
  const priority = priorityValue && priorities.has(priorityValue as BacklogPriorityFilter)
    ? priorityValue as BacklogPriorityFilter
    : "all";
  const sourceValue = single(input.sourcePendingActionId);
  const sourcePendingActionId = sourceValue && isNonEmptyGuid(sourceValue)
    ? sourceValue
    : undefined;

  return { page, status, priority, sourcePendingActionId };
}

export function backlogHref(
  filters: BacklogFilters,
  overrides: Partial<BacklogFilters> = {},
): string {
  const next = { ...filters, ...overrides };
  const params = new URLSearchParams();
  if (next.sourcePendingActionId) {
    params.set("sourcePendingActionId", next.sourcePendingActionId);
  }
  if (next.status !== "all") params.set("status", next.status);
  if (next.priority !== "all") params.set("priority", next.priority);
  if (next.page > 1) params.set("page", String(next.page));
  const query = params.toString();
  return query ? `/backlog?${query}` : "/backlog";
}

function single(value: string | string[] | undefined): string | undefined {
  return typeof value === "string" ? value : undefined;
}

function isNonEmptyGuid(value: string): boolean {
  return guidPattern.test(value)
    && value.toLowerCase() !== "00000000-0000-0000-0000-000000000000";
}
