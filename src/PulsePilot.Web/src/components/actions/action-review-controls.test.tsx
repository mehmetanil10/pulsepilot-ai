// @vitest-environment jsdom

import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

const router = vi.hoisted(() => ({ replace: vi.fn(), refresh: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => router }));

import { ActionReviewControls } from "./action-review-controls";

describe("ActionReviewControls", () => {
  beforeEach(() => {
    router.replace.mockReset();
    router.refresh.mockReset();
  });

  it("keeps workspace members in read-only mode", () => {
    render(<ActionReviewControls actionId="action-1" actionType="createEngineeringIssue" canReview={false} />);

    expect(screen.getByText("Admin review required")).toBeDefined();
    expect(screen.queryByRole("button", { name: "Approve" })).toBeNull();
  });

  it("requires confirmation and records an approved decision", async () => {
    const fetchMock = vi.fn().mockResolvedValue(Response.json({}, { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);
    render(<ActionReviewControls actionId="action-1" actionType="createEngineeringIssue" canReview />);

    fireEvent.click(screen.getByRole("button", { name: /Approve/ }));
    expect(screen.getByText("Approve this recommendation?")).toBeDefined();
    expect(screen.getByText(/create one backlog item/)).toBeDefined();
    fireEvent.click(screen.getByRole("button", { name: "Confirm approval" }));

    await waitFor(() => expect(router.refresh).toHaveBeenCalledOnce());
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/backend/actions/action-1/approve",
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("surfaces concurrent-review conflicts and refreshes current state", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(Response.json({
      title: "Conflict",
      status: 409,
    }, { status: 409 })));
    render(<ActionReviewControls actionId="action-2" actionType="draftCustomerResponse" canReview />);

    fireEvent.click(screen.getByRole("button", { name: "Reject" }));
    fireEvent.click(screen.getByRole("button", { name: "Confirm rejection" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("already reviewed");
    expect(router.refresh).toHaveBeenCalledOnce();
  });
});
