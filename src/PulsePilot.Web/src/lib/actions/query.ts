import type { ActionStatusFilter, PendingActionFilters } from "@/types/actions";

export const actionPageSize = 10;

const statuses = new Set<ActionStatusFilter>([
  "pending",
  "approved",
  "rejected",
  "executed",
  "failed",
  "all",
]);

export function parsePendingActionFilters(
  input: Record<string, string | string[] | undefined>,
): PendingActionFilters {
  const pageValue = single(input.page);
  const parsedPage = pageValue ? Number(pageValue) : 1;
  const page = Number.isSafeInteger(parsedPage) && parsedPage > 0 ? parsedPage : 1;
  const statusValue = single(input.status);
  const status = statusValue && statuses.has(statusValue as ActionStatusFilter)
    ? statusValue as ActionStatusFilter
    : "pending";

  return { page, status };
}

export function pendingActionHref(
  filters: PendingActionFilters,
  overrides: Partial<PendingActionFilters> = {},
): string {
  const next = { ...filters, ...overrides };
  const params = new URLSearchParams();
  if (next.status !== "pending") params.set("status", next.status);
  if (next.page > 1) params.set("page", String(next.page));
  const query = params.toString();
  return query ? `/actions?${query}` : "/actions";
}

function single(value: string | string[] | undefined): string | undefined {
  return typeof value === "string" ? value : undefined;
}
