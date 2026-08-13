import "server-only";

import { readSessionToken } from "@/lib/auth/session";
import { parseBacklogList } from "@/lib/backlog/parser";
import { backlogPageSize } from "@/lib/backlog/query";
import { getApiBaseUrl } from "@/lib/env";
import type { BacklogFilters, BacklogListPage } from "@/types/backlog";

export type BacklogListResult =
  | { ok: true; data: BacklogListPage }
  | { ok: false; status: number };

export async function getBacklogList(filters: BacklogFilters): Promise<BacklogListResult> {
  const token = await readSessionToken();
  if (!token) return { ok: false, status: 401 };

  const url = new URL("api/backlog", getApiBaseUrl());
  url.searchParams.set("page", String(filters.page));
  url.searchParams.set("pageSize", String(backlogPageSize));
  if (filters.status !== "all") url.searchParams.set("status", filters.status);
  if (filters.priority !== "all") url.searchParams.set("priority", filters.priority);
  if (filters.sourcePendingActionId) {
    url.searchParams.set("sourcePendingActionId", filters.sourcePendingActionId);
  }

  try {
    const response = await fetch(url, {
      headers: { Accept: "application/json", Authorization: `Bearer ${token}` },
      cache: "no-store",
      signal: AbortSignal.timeout(15_000),
    });
    if (!response.ok) return { ok: false, status: response.status };

    const data = parseBacklogList(await response.json());
    return data ? { ok: true, data } : { ok: false, status: 502 };
  } catch {
    return { ok: false, status: 503 };
  }
}
