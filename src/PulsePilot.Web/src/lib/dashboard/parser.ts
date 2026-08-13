import type {
  DashboardCategoryCount,
  DashboardPendingAction,
  DashboardRecentFeedback,
  DashboardSummary,
  DashboardTrending,
  DashboardTrendingIssue,
} from "@/types/dashboard";

export function parseDashboardSummary(value: unknown): DashboardSummary | null {
  if (!isRecord(value) || !isRecord(value.kpis)) return null;

  const kpis = value.kpis;
  if (
    !isIsoDate(value.generatedAt) ||
    !isIsoDate(value.periodFromInclusive) ||
    !isNonNegativeInteger(value.periodDays) ||
    !isNonNegativeInteger(kpis.feedbackToday) ||
    !isNonNegativeInteger(kpis.aiProcessed) ||
    !isNonNegativeInteger(kpis.criticalIssues) ||
    !isNonNegativeInteger(kpis.pendingActions) ||
    !isNonNegativeInteger(kpis.processingFailures) ||
    !(kpis.averageSeverity === null || isFiniteNumber(kpis.averageSeverity))
  ) return null;

  const categories = parseArray(value.categories, parseCategory);
  const recentFeedback = parseArray(value.recentFeedback, parseRecentFeedback);
  const pendingActions = parseArray(value.pendingActions, parsePendingAction);
  if (!categories || !recentFeedback || !pendingActions) return null;

  return {
    generatedAt: value.generatedAt,
    periodFromInclusive: value.periodFromInclusive,
    periodDays: value.periodDays,
    kpis: {
      feedbackToday: kpis.feedbackToday,
      aiProcessed: kpis.aiProcessed,
      criticalIssues: kpis.criticalIssues,
      pendingActions: kpis.pendingActions,
      processingFailures: kpis.processingFailures,
      averageSeverity: kpis.averageSeverity,
    },
    categories,
    recentFeedback,
    pendingActions,
  };
}

export function parseDashboardTrending(value: unknown): DashboardTrending | null {
  if (
    !isRecord(value) ||
    !isIsoDate(value.previousFromInclusive) ||
    !isIsoDate(value.currentFromInclusive) ||
    !isIsoDate(value.currentToExclusive) ||
    !isNonNegativeInteger(value.periodDays)
  ) return null;

  const items = parseArray(value.items, parseTrendingIssue);
  return items ? {
    previousFromInclusive: value.previousFromInclusive,
    currentFromInclusive: value.currentFromInclusive,
    currentToExclusive: value.currentToExclusive,
    periodDays: value.periodDays,
    items,
  } : null;
}

function parseCategory(value: unknown): DashboardCategoryCount | null {
  return isRecord(value) && isString(value.category) && isNonNegativeInteger(value.count)
    ? { category: value.category, count: value.count }
    : null;
}

function parseRecentFeedback(value: unknown): DashboardRecentFeedback | null {
  return isRecord(value) && isString(value.id)
    && (value.title === null || isString(value.title))
    && isString(value.source) && isString(value.processingStatus)
    && isIsoDate(value.createdAt)
    ? {
        id: value.id,
        title: value.title,
        source: value.source,
        processingStatus: value.processingStatus,
        createdAt: value.createdAt,
      }
    : null;
}

function parsePendingAction(value: unknown): DashboardPendingAction | null {
  return isRecord(value) && isString(value.id) && isString(value.actionType)
    && isString(value.title) && isString(value.description) && isIsoDate(value.createdAt)
    ? {
        id: value.id,
        actionType: value.actionType,
        title: value.title,
        description: value.description,
        createdAt: value.createdAt,
      }
    : null;
}

function parseTrendingIssue(value: unknown): DashboardTrendingIssue | null {
  return isRecord(value) && isString(value.feedbackClusterId) && isString(value.title)
    && isString(value.category) && isString(value.component) && isString(value.priority)
    && isFiniteNumber(value.priorityScore) && isNonNegativeInteger(value.currentPeriodCount)
    && isNonNegativeInteger(value.previousPeriodCount)
    && isFiniteNumber(value.deltaCount) && Number.isInteger(value.deltaCount)
    && (value.growthPercentage === null || isFiniteNumber(value.growthPercentage))
    && typeof value.isNew === "boolean"
    ? {
        feedbackClusterId: value.feedbackClusterId,
        title: value.title,
        category: value.category,
        component: value.component,
        priority: value.priority,
        priorityScore: value.priorityScore,
        currentPeriodCount: value.currentPeriodCount,
        previousPeriodCount: value.previousPeriodCount,
        deltaCount: value.deltaCount,
        growthPercentage: value.growthPercentage,
        isNew: value.isNew,
      }
    : null;
}

function parseArray<T>(value: unknown, parse: (item: unknown) => T | null): T[] | null {
  if (!Array.isArray(value) || value.length > 100) return null;
  const result = value.map(parse);
  return result.some((item) => item === null) ? null : result as T[];
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

function isString(value: unknown): value is string {
  return typeof value === "string" && value.length <= 2_000;
}

function isFiniteNumber(value: unknown): value is number {
  return typeof value === "number" && Number.isFinite(value);
}

function isNonNegativeInteger(value: unknown): value is number {
  return isFiniteNumber(value) && Number.isInteger(value) && value >= 0;
}

function isIsoDate(value: unknown): value is string {
  return isString(value) && !Number.isNaN(Date.parse(value));
}
