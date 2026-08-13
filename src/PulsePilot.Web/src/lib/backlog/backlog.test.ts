import { describe, expect, it } from "vitest";

import { parseBacklogList } from "./parser";
import { backlogHref, parseBacklogFilters } from "./query";

const sourceActionId = "01989f20-461e-7b5b-a95a-cf872b6f2011";
const backlogItem = {
  id: "01989f20-461e-7b5b-a95a-cf872b6f2012",
  sourceClusterId: "01989f20-461e-7b5b-a95a-cf872b6f2013",
  sourcePendingActionId: sourceActionId,
  createdByUserId: "01989f20-461e-7b5b-a95a-cf872b6f2014",
  title: "[P1] Checkout failures",
  description: "Trace the payment callback and add a regression test.",
  priority: "p1",
  status: "open",
  createdAt: "2026-08-13T12:00:00Z",
  updatedAt: "2026-08-13T12:05:00Z",
};

describe("backlog response parser", () => {
  it("accepts a bounded backlog page", () => {
    expect(parseBacklogList({
      items: [backlogItem],
      page: 1,
      pageSize: 12,
      totalCount: 1,
    })?.items[0]).toMatchObject({
      sourcePendingActionId: sourceActionId,
      priority: "p1",
      status: "open",
    });
  });

  it("rejects unknown lifecycle values and malformed timestamps", () => {
    expect(parseBacklogList({
      items: [{ ...backlogItem, status: "blocked" }],
      page: 1,
      pageSize: 12,
      totalCount: 1,
    })).toBeNull();
    expect(parseBacklogList({
      items: [{ ...backlogItem, updatedAt: "recently" }],
      page: 1,
      pageSize: 12,
      totalCount: 1,
    })).toBeNull();
  });

  it("rejects empty identifiers and oversized result sets", () => {
    expect(parseBacklogList({
      items: [{ ...backlogItem, id: "00000000-0000-0000-0000-000000000000" }],
      page: 1,
      pageSize: 12,
      totalCount: 1,
    })).toBeNull();
    expect(parseBacklogList({
      items: Array.from({ length: 101 }, () => backlogItem),
      page: 1,
      pageSize: 101,
      totalCount: 101,
    })).toBeNull();
  });
});

describe("backlog filters", () => {
  it("defaults to the whole backlog and emits compact links", () => {
    expect(parseBacklogFilters({})).toEqual({
      page: 1,
      status: "all",
      priority: "all",
      sourcePendingActionId: undefined,
    });
    expect(backlogHref({ page: 1, status: "all", priority: "all" }))
      .toBe("/backlog");
  });

  it("keeps supported filters and the source action trace", () => {
    const filters = parseBacklogFilters({
      page: "2",
      status: "inProgress",
      priority: "p2",
      sourcePendingActionId: sourceActionId,
    });

    expect(filters).toEqual({
      page: 2,
      status: "inProgress",
      priority: "p2",
      sourcePendingActionId: sourceActionId,
    });
    expect(backlogHref(filters)).toBe(
      `/backlog?sourcePendingActionId=${sourceActionId}&status=inProgress&priority=p2&page=2`,
    );
  });

  it("drops arrays, unsupported values, and malformed source ids", () => {
    expect(parseBacklogFilters({
      page: ["2"],
      status: "blocked",
      priority: "urgent",
      sourcePendingActionId: "not-a-guid",
    })).toEqual({
      page: 1,
      status: "all",
      priority: "all",
      sourcePendingActionId: undefined,
    });
  });
});
