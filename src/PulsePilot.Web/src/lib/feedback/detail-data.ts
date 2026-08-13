import "server-only";

import { readSessionToken } from "@/lib/auth/session";
import { getApiBaseUrl } from "@/lib/env";
import {
  parseFeedbackAnalysis,
  parseFeedbackCluster,
  parseFeedbackDetail,
  parseSimilarFeedback,
} from "@/lib/feedback/parser";
import type { FeedbackDetailBundle } from "@/types/feedback";

export type FeedbackDetailResult =
  | { ok: true; data: FeedbackDetailBundle }
  | { ok: false; status: number };

type ApiResult = { status: number; value?: unknown };

export async function getFeedbackDetail(id: string): Promise<FeedbackDetailResult> {
  const token = await readSessionToken();
  if (!token) return { ok: false, status: 401 };

  const encodedId = encodeURIComponent(id);
  const [feedbackResult, analysisResult, similarResult] = await Promise.all([
    getApiResource(`api/feedback/${encodedId}`, token),
    getApiResource(`api/feedback/${encodedId}/analysis`, token),
    getApiResource(`api/feedback/${encodedId}/similar?limit=6`, token),
  ]);

  if ([feedbackResult, analysisResult, similarResult]
    .some((result) => result.status === 401)) return { ok: false, status: 401 };
  if (feedbackResult.status !== 200) {
    return { ok: false, status: feedbackResult.status };
  }

  const feedback = parseFeedbackDetail(feedbackResult.value);
  if (!feedback) return { ok: false, status: 502 };

  const parsedAnalysis = analysisResult.status === 200
    ? parseFeedbackAnalysis(analysisResult.value)
    : null;
  const analysis = parsedAnalysis?.feedbackId === feedback.id ? parsedAnalysis : null;
  const analysisState = analysisResult.status === 200 && analysis
    ? "ready" as const
    : "unavailable" as const;

  const parsedSimilar = similarResult.status === 200
    ? parseSimilarFeedback(similarResult.value)
    : null;
  const similarFeedback = parsedSimilar?.feedbackId === feedback.id ? parsedSimilar : null;
  const similarState = similarResult.status === 409
    ? "blocked" as const
    : similarResult.status === 200 && similarFeedback
      ? "ready" as const
      : "unavailable" as const;

  const clusterResult = feedback.feedbackClusterId
    ? await getApiResource(
      `api/clusters/${encodeURIComponent(feedback.feedbackClusterId)}?page=1&pageSize=1`,
      token,
    )
    : null;
  if (clusterResult?.status === 401) return { ok: false, status: 401 };

  const parsedCluster = clusterResult?.status === 200
    ? parseFeedbackCluster(clusterResult.value)
    : null;
  const cluster = parsedCluster?.id === feedback.feedbackClusterId ? parsedCluster : null;
  const clusterState = !feedback.feedbackClusterId
    ? "missing" as const
    : clusterResult?.status === 200 && cluster
      ? "ready" as const
      : "unavailable" as const;

  return {
    ok: true,
    data: {
      feedback,
      analysis,
      analysisState,
      similarFeedback,
      similarState,
      cluster,
      clusterState,
    },
  };
}

async function getApiResource(path: string, token: string): Promise<ApiResult> {
  try {
    const response = await fetch(new URL(path, getApiBaseUrl()), {
      headers: { Accept: "application/json", Authorization: `Bearer ${token}` },
      cache: "no-store",
      signal: AbortSignal.timeout(15_000),
    });
    if (!response.ok) return { status: response.status };

    try {
      return { status: response.status, value: await response.json() };
    } catch {
      return { status: 502 };
    }
  } catch {
    return { status: 503 };
  }
}
