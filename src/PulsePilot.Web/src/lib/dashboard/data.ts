import "server-only";

import { getApiBaseUrl } from "@/lib/env";
import { readSessionToken } from "@/lib/auth/session";
import { parseDashboardSummary, parseDashboardTrending } from "@/lib/dashboard/parser";
import type { DashboardData } from "@/types/dashboard";

export type DashboardDataResult =
  | { ok: true; data: DashboardData }
  | { ok: false; status: number };

export async function getDashboardData(periodDays: number): Promise<DashboardDataResult> {
  const token = await readSessionToken();
  if (!token) return { ok: false, status: 401 };

  const summaryUrl = new URL("api/dashboard/summary", getApiBaseUrl());
  summaryUrl.search = new URLSearchParams({
    periodDays: String(periodDays),
    recentFeedbackLimit: "5",
    pendingActionLimit: "4",
  }).toString();
  const trendingUrl = new URL("api/dashboard/trending", getApiBaseUrl());
  trendingUrl.search = new URLSearchParams({
    periodDays: String(periodDays),
    limit: "5",
  }).toString();

  const request = (url: URL) => fetch(url, {
    headers: { Accept: "application/json", Authorization: `Bearer ${token}` },
    cache: "no-store",
    signal: AbortSignal.timeout(15_000),
  });

  try {
    const [summaryResponse, trendingResponse] = await Promise.all([
      request(summaryUrl),
      request(trendingUrl),
    ]);
    if (!summaryResponse.ok || !trendingResponse.ok) {
      return {
        ok: false,
        status: summaryResponse.ok ? trendingResponse.status : summaryResponse.status,
      };
    }

    const [summaryValue, trendingValue] = await Promise.all([
      summaryResponse.json(),
      trendingResponse.json(),
    ]);
    const summary = parseDashboardSummary(summaryValue);
    const trending = parseDashboardTrending(trendingValue);
    return summary && trending
      ? { ok: true, data: { summary, trending } }
      : { ok: false, status: 502 };
  } catch {
    return { ok: false, status: 503 };
  }
}
