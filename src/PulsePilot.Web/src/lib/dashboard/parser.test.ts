import { describe, expect, it } from "vitest";

import { parseDashboardSummary, parseDashboardTrending } from "./parser";

const summary = {
  generatedAt: "2026-08-13T12:00:00Z",
  periodFromInclusive: "2026-08-06T12:00:00Z",
  periodDays: 7,
  kpis: {
    feedbackToday: 4,
    aiProcessed: 12,
    criticalIssues: 2,
    pendingActions: 3,
    processingFailures: 1,
    averageSeverity: 3.5,
  },
  categories: [{ category: "bug", count: 5 }],
  recentFeedback: [{
    id: "feedback-id",
    title: "Checkout failure",
    source: "manual",
    processingStatus: "completed",
    createdAt: "2026-08-13T11:00:00Z",
  }],
  pendingActions: [{
    id: "action-id",
    actionType: "createEngineeringIssue",
    title: "Create issue",
    description: "Review checkout failures.",
    createdAt: "2026-08-13T10:00:00Z",
  }],
};

describe("dashboard response parsers", () => {
  it("accepts complete bounded dashboard data", () => {
    expect(parseDashboardSummary(summary)).toMatchObject({ periodDays: 7 });
    expect(parseDashboardTrending({
      previousFromInclusive: "2026-07-30T12:00:00Z",
      currentFromInclusive: "2026-08-06T12:00:00Z",
      currentToExclusive: "2026-08-13T12:00:00Z",
      periodDays: 7,
      items: [{
        feedbackClusterId: "cluster-id",
        title: "Checkout failures",
        category: "bug",
        component: "payments",
        priority: "p1",
        priorityScore: 91,
        currentPeriodCount: 5,
        previousPeriodCount: 2,
        deltaCount: 3,
        growthPercentage: 150,
        isNew: false,
      }],
    })?.items).toHaveLength(1);
  });

  it("rejects malformed or unbounded response shapes", () => {
    expect(parseDashboardSummary({ ...summary, kpis: { ...summary.kpis, feedbackToday: -1 } }))
      .toBeNull();
    expect(parseDashboardTrending({ items: [] })).toBeNull();
  });
});
