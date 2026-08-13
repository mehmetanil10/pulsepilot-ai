import { describe, expect, it } from "vitest";

import { parseFeedbackListPage } from "./parser";
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
