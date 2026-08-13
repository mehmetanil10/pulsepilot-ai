import "server-only";

import { readSessionToken } from "@/lib/auth/session";
import { getApiBaseUrl } from "@/lib/env";
import { parsePendingActionList } from "@/lib/actions/parser";
import { actionPageSize } from "@/lib/actions/query";
import type { PendingActionFilters, PendingActionListPage } from "@/types/actions";

export type PendingActionListResult =
  | { ok: true; data: PendingActionListPage }
  | { ok: false; status: number };

export async function getPendingActionList(
  filters: PendingActionFilters,
): Promise<PendingActionListResult> {
  const token = await readSessionToken();
  if (!token) return { ok: false, status: 401 };

  const url = new URL("api/actions", getApiBaseUrl());
  url.searchParams.set("page", String(filters.page));
  url.searchParams.set("pageSize", String(actionPageSize));
  if (filters.status !== "all") url.searchParams.set("status", filters.status);

  try {
    const response = await fetch(url, {
      headers: { Accept: "application/json", Authorization: `Bearer ${token}` },
      cache: "no-store",
      signal: AbortSignal.timeout(15_000),
    });
    if (!response.ok) return { ok: false, status: response.status };

    const data = parsePendingActionList(await response.json());
    return data ? { ok: true, data } : { ok: false, status: 502 };
  } catch {
    return { ok: false, status: 503 };
  }
}
