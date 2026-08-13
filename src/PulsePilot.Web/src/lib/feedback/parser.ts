import type {
  FeedbackAnalysis,
  FeedbackAnalysisResult,
  FeedbackCluster,
  FeedbackDetail,
  FeedbackListItem,
  FeedbackListPage,
  SimilarFeedback,
  SimilarFeedbackItem,
} from "@/types/feedback";

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

export function parseFeedbackDetail(value: unknown): FeedbackDetail | null {
  if (!isRecord(value)
    || !isString(value.id, 100)
    || !(value.feedbackClusterId === null || isString(value.feedbackClusterId, 100))
    || !(value.title === null || isString(value.title, 500))
    || !isString(value.content, 20_000)
    || !isString(value.source, 50)
    || !isString(value.processingStatus, 50)
    || !isIsoDate(value.createdAt)
    || !isIsoDate(value.updatedAt)) return null;

  return {
    id: value.id,
    feedbackClusterId: value.feedbackClusterId,
    title: value.title,
    content: value.content,
    source: value.source,
    processingStatus: value.processingStatus,
    createdAt: value.createdAt,
    updatedAt: value.updatedAt,
  };
}

export function parseFeedbackAnalysis(value: unknown): FeedbackAnalysis | null {
  if (!isRecord(value)
    || !isString(value.feedbackId, 100)
    || !isString(value.processingStatus, 50)
    || typeof value.isCurrent !== "boolean"
    || !(value.analysis === null || isRecord(value.analysis))) return null;

  const analysis = value.analysis === null
    ? null
    : parseFeedbackAnalysisResult(value.analysis);
  if (value.analysis !== null && analysis === null) return null;

  return {
    feedbackId: value.feedbackId,
    processingStatus: value.processingStatus,
    isCurrent: value.isCurrent,
    analysis,
  };
}

export function parseSimilarFeedback(value: unknown): SimilarFeedback | null {
  if (!isRecord(value)
    || !isString(value.feedbackId, 100)
    || !isUnitInterval(value.similarityThreshold)
    || !Array.isArray(value.items)
    || value.items.length > 50) return null;

  const items = value.items.map(parseSimilarFeedbackItem);
  if (items.some((item) => item === null)) return null;

  return {
    feedbackId: value.feedbackId,
    similarityThreshold: value.similarityThreshold,
    items: items as SimilarFeedbackItem[],
  };
}

export function parseFeedbackCluster(value: unknown): FeedbackCluster | null {
  if (!isRecord(value)
    || !isString(value.id, 100)
    || !isString(value.title, 500)
    || !isString(value.category, 50)
    || !isString(value.component, 50)
    || !isNumberBetween(value.priorityScore, 0, 100)
    || !isString(value.priority, 20)
    || !isNonNegativeInteger(value.totalFeedbackCount)
    || !isIsoDate(value.createdAt)
    || !isIsoDate(value.updatedAt)) return null;

  return {
    id: value.id,
    title: value.title,
    category: value.category,
    component: value.component,
    priorityScore: value.priorityScore,
    priority: value.priority,
    totalFeedbackCount: value.totalFeedbackCount,
    createdAt: value.createdAt,
    updatedAt: value.updatedAt,
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

function parseFeedbackAnalysisResult(value: Record<string, unknown>): FeedbackAnalysisResult | null {
  if (!isString(value.id, 100)
    || !isString(value.category, 50)
    || !isString(value.component, 50)
    || !isSeverity(value.severity)
    || !isString(value.sentiment, 50)
    || !isString(value.summary, 4_000)
    || !isString(value.suggestedAction, 4_000)
    || !isUnitInterval(value.confidence)
    || !isIsoDate(value.createdAt)
    || !isIsoDate(value.updatedAt)) return null;

  return {
    id: value.id,
    category: value.category,
    component: value.component,
    severity: value.severity,
    sentiment: value.sentiment,
    summary: value.summary,
    suggestedAction: value.suggestedAction,
    confidence: value.confidence,
    createdAt: value.createdAt,
    updatedAt: value.updatedAt,
  };
}

function parseSimilarFeedbackItem(value: unknown): SimilarFeedbackItem | null {
  if (!isRecord(value)
    || !isString(value.id, 100)
    || !(value.feedbackClusterId === null || isString(value.feedbackClusterId, 100))
    || !(value.title === null || isString(value.title, 500))
    || !isString(value.content, 20_000)
    || !isString(value.source, 50)
    || !isUnitInterval(value.similarity)
    || !isIsoDate(value.createdAt)) return null;

  return {
    id: value.id,
    feedbackClusterId: value.feedbackClusterId,
    title: value.title,
    content: value.content,
    source: value.source,
    similarity: value.similarity,
    createdAt: value.createdAt,
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

function isNumberBetween(value: unknown, minimum: number, maximum: number): value is number {
  return typeof value === "number"
    && Number.isFinite(value)
    && value >= minimum
    && value <= maximum;
}

function isUnitInterval(value: unknown): value is number {
  return isNumberBetween(value, 0, 1);
}

function isIsoDate(value: unknown): value is string {
  return isString(value, 100) && !Number.isNaN(Date.parse(value));
}
