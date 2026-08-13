import { describe, expect, it } from "vitest";

import { parsePendingActionList } from "./parser";
import { parsePendingActionFilters, pendingActionHref } from "./query";

const action = {
  id: "action-id",
  feedbackId: "feedback-id",
  feedbackClusterId: "cluster-id",
  actionType: "createEngineeringIssue",
  title: "[P1] Checkout failures",
  description: "Create an engineering issue for the P1 checkout cluster.",
  payload: {
    feedbackId: "feedback-id",
    feedbackClusterId: "cluster-id",
    priority: "p1",
    priorityScore: 87.5,
    category: "bug",
    component: "payments",
    feedbackCount: 14,
    suggestedAction: "Trace the confirmation callback and add a regression test.",
    internalValue: "must not enter the web projection",
  },
  status: "pending",
  approvedAt: null,
  rejectedAt: null,
  executedAt: null,
  createdAt: "2026-08-13T12:00:00Z",
  updatedAt: "2026-08-13T12:00:00Z",
};

describe("pending action response parser", () => {
  it("projects a bounded action and only allowlisted context fields", () => {
    const parsed = parsePendingActionList({
      items: [action],
      page: 1,
      pageSize: 10,
      totalCount: 1,
    });

    expect(parsed?.items[0]).toMatchObject({
      actionType: "createEngineeringIssue",
      status: "pending",
      context: { priority: "p1", priorityScore: 87.5, feedbackCount: 14 },
    });
    expect(parsed?.items[0].context).not.toHaveProperty("internalValue");
  });

  it("keeps legacy payload context optional without dropping the action", () => {
    const parsed = parsePendingActionList({
      items: [{ ...action, payload: { priority: "urgent", priorityScore: 120 } }],
      page: 1,
      pageSize: 10,
      totalCount: 1,
    });

    expect(parsed?.items[0].context).toEqual({
      priority: null,
      priorityScore: null,
      category: null,
      component: null,
      feedbackCount: null,
      suggestedAction: null,
    });
  });

  it("rejects unsupported action state and malformed review timestamps", () => {
    expect(parsePendingActionList({
      items: [{ ...action, status: "running" }],
      page: 1,
      pageSize: 10,
      totalCount: 1,
    })).toBeNull();
    expect(parsePendingActionList({
      items: [{ ...action, approvedAt: "yesterday" }],
      page: 1,
      pageSize: 10,
      totalCount: 1,
    })).toBeNull();
  });
});

describe("pending action filters", () => {
  it("defaults to the pending queue and emits compact links", () => {
    expect(parsePendingActionFilters({})).toEqual({ page: 1, status: "pending" });
    expect(pendingActionHref({ page: 1, status: "pending" })).toBe("/actions");
    expect(pendingActionHref({ page: 2, status: "executed" }))
      .toBe("/actions?status=executed&page=2");
  });

  it("drops malformed pages and unsupported statuses", () => {
    expect(parsePendingActionFilters({ page: "-4", status: "running" }))
      .toEqual({ page: 1, status: "pending" });
    expect(parsePendingActionFilters({ page: ["2"], status: ["all"] }))
      .toEqual({ page: 1, status: "pending" });
  });
});
