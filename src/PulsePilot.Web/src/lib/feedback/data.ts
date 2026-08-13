import "server-only";

import { readSessionToken } from "@/lib/auth/session";
import { getApiBaseUrl } from "@/lib/env";
import { parseFeedbackListPage } from "@/lib/feedback/parser";
import { feedbackPageSize } from "@/lib/feedback/query";
import type { FeedbackListFilters, FeedbackListPage } from "@/types/feedback";

export type FeedbackListResult =
  | { ok: true; data: FeedbackListPage }
  | { ok: false; status: number };

export async function getFeedbackList(
  filters: FeedbackListFilters,
): Promise<FeedbackListResult> {
  const token = await readSessionToken();
  if (!token) return { ok: false, status: 401 };

  const url = new URL("api/feedback", getApiBaseUrl());
  const params = new URLSearchParams({
    page: String(filters.page),
    pageSize: String(feedbackPageSize),
  });

  for (const [key, value] of Object.entries(filters)) {
    if (key === "page" || value === undefined) continue;
    params.set(key, String(value));
  }
  url.search = params.toString();

  try {
    const response = await fetch(url, {
      headers: { Accept: "application/json", Authorization: `Bearer ${token}` },
      cache: "no-store",
      signal: AbortSignal.timeout(15_000),
    });
    if (!response.ok) return { ok: false, status: response.status };

    const data = parseFeedbackListPage(await response.json());
    return data ? { ok: true, data } : { ok: false, status: 502 };
  } catch {
    return { ok: false, status: 503 };
  }
}
