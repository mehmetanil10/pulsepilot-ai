import { describe, expect, it } from "vitest";

import {
  parseFeedbackAnalysis,
  parseFeedbackCluster,
  parseFeedbackDetail,
  parseFeedbackListPage,
  parseSimilarFeedback,
} from "./parser";
import { countActiveFeedbackFilters, feedbackHref, parseFeedbackFilters } from "./query";

const page = {
  items: [{
    id: "feedback-id",
    feedbackClusterId: null,
    title: "Checkout failure",
    content: "Payment confirmation never completes.",
    source: "support",
    processingStatus: "completed",
    createdAt: "2026-08-13T12:00:00Z",
    updatedAt: "2026-08-13T12:01:00Z",
    category: "bug",
    component: "payments",
    severity: 5,
    sentiment: "negative",
  }],
  page: 1,
  pageSize: 12,
  totalCount: 1,
};

describe("feedback list response parser", () => {
  it("accepts the complete bounded list shape", () => {
    expect(parseFeedbackListPage(page)).toMatchObject({ totalCount: 1 });
  });

  it("rejects invalid severity and oversized collections", () => {
    expect(parseFeedbackListPage({
      ...page,
      items: [{ ...page.items[0], severity: 8 }],
    })).toBeNull();
    expect(parseFeedbackListPage({ ...page, items: Array(101).fill(page.items[0]) }))
      .toBeNull();
  });
});

describe("feedback detail response parsers", () => {
  const detail = {
    id: "feedback-id",
    workspaceId: "workspace-id",
    feedbackClusterId: "cluster-id",
    createdByUserId: "user-id",
    title: "Checkout failure",
    content: "Payment confirmation never completes.",
    source: "support",
    customerName: "Not exposed by the web model",
    customerEmail: "not-exposed@example.test",
    processingStatus: "completed",
    createdAt: "2026-08-13T12:00:00Z",
    updatedAt: "2026-08-13T12:01:00Z",
  };

  const analysis = {
    feedbackId: "feedback-id",
    processingStatus: "completed",
    isCurrent: true,
    analysis: {
      id: "analysis-id",
      category: "bug",
      component: "payments",
      severity: 5,
      sentiment: "negative",
      summary: "Checkout cannot complete after payment confirmation.",
      suggestedAction: "Inspect confirmation callbacks and add a regression test.",
      confidence: 0.96,
      createdAt: "2026-08-13T12:00:30Z",
      updatedAt: "2026-08-13T12:00:30Z",
    },
  };

  it("accepts the PII-free detail projection and structured analysis", () => {
    expect(parseFeedbackDetail(detail)).toEqual({
      id: "feedback-id",
      feedbackClusterId: "cluster-id",
      title: "Checkout failure",
      content: "Payment confirmation never completes.",
      source: "support",
      processingStatus: "completed",
      createdAt: "2026-08-13T12:00:00Z",
      updatedAt: "2026-08-13T12:01:00Z",
    });
    expect(parseFeedbackAnalysis(analysis)?.analysis).toMatchObject({ severity: 5, confidence: 0.96 });
  });

  it("accepts bounded semantic matches and cluster metadata", () => {
    expect(parseSimilarFeedback({
      feedbackId: "feedback-id",
      similarityThreshold: 0.8,
      items: [{
        id: "related-id",
        feedbackClusterId: "cluster-id",
        title: "Payment stuck",
        content: "The spinner remains after checkout.",
        source: "survey",
        similarity: 0.91,
        createdAt: "2026-08-12T09:00:00Z",
      }],
      count: 1,
    })?.items).toHaveLength(1);

    expect(parseFeedbackCluster({
      id: "cluster-id",
      title: "Checkout confirmation failures",
      category: "bug",
      component: "payments",
      priorityScore: 82.5,
      priority: "p1",
      feedback: [],
      page: 1,
      pageSize: 1,
      totalFeedbackCount: 8,
      createdAt: "2026-08-11T09:00:00Z",
      updatedAt: "2026-08-13T11:00:00Z",
    })).toMatchObject({ priority: "p1", totalFeedbackCount: 8 });
  });

  it("rejects out-of-range model confidence, similarity, and priority scores", () => {
    expect(parseFeedbackAnalysis({
      ...analysis,
      analysis: { ...analysis.analysis, confidence: 1.1 },
    })).toBeNull();
    expect(parseSimilarFeedback({
      feedbackId: "feedback-id",
      similarityThreshold: 0.8,
      items: [{
        id: "related-id",
        feedbackClusterId: null,
        title: null,
        content: "Related feedback",
        source: "manual",
        similarity: -0.1,
        createdAt: "2026-08-12T09:00:00Z",
      }],
    })).toBeNull();
    expect(parseFeedbackCluster({
      id: "cluster-id",
      title: "Cluster",
      category: "bug",
      component: "payments",
      priorityScore: 101,
      priority: "p1",
      totalFeedbackCount: 1,
      createdAt: "2026-08-11T09:00:00Z",
      updatedAt: "2026-08-13T11:00:00Z",
    })).toBeNull();
  });
});

describe("feedback URL filters", () => {
  it("normalizes supported filters and builds stable pagination links", () => {
    const filters = parseFeedbackFilters({
      page: "2",
      source: "support",
      category: "bug",
      severity: "5",
      dateFrom: "2026-08-01",
      dateTo: "2026-08-13",
      search: "  checkout  ",
    });

    expect(filters).toEqual({
      page: 2,
      source: "support",
      category: "bug",
      severity: 5,
      dateFrom: "2026-08-01",
      dateTo: "2026-08-13",
      search: "checkout",
    });
    expect(countActiveFeedbackFilters(filters)).toBe(6);
    expect(feedbackHref(filters, { page: 3 }))
      .toContain("page=3");
  });

  it("drops malformed values and reversed date ranges", () => {
    expect(parseFeedbackFilters({
      page: "-1",
      source: "webhook",
      severity: "9",
      dateFrom: "2026-08-13",
      dateTo: "2026-08-01",
    })).toEqual({ page: 1 });
  });
});
