// @vitest-environment jsdom

import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

const router = vi.hoisted(() => ({ replace: vi.fn(), refresh: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => router }));

import { CopilotChat } from "./copilot-chat";

const validAnswer = {
  answer: "Payment failures are the fastest-growing issue.",
  modelTurnCount: 2,
  toolCallCount: 1,
  toolUsages: [{ toolName: "get_trending_issues", succeeded: true }],
};

describe("CopilotChat", () => {
  beforeEach(() => {
    router.replace.mockReset();
    router.refresh.mockReset();
  });

  it("validates questions before contacting the backend", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    render(<CopilotChat />);

    fireEvent.submit(screen.getByLabelText(/Ask about feedback/).closest("form")!);

    expect(await screen.findByText("Ask PulsePilot a product question first.")).toBeDefined();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("renders grounded answers and a safe tool summary", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(Response.json(validAnswer)));
    render(<CopilotChat />);

    fireEvent.change(screen.getByLabelText(/Ask about feedback/), {
      target: { value: "What changed this week?" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Ask Copilot" }));

    expect(await screen.findByText(validAnswer.answer)).toBeDefined();
    expect(screen.getByText("Trending issues")).toBeDefined();
    expect(screen.getByText(/2 model turns.*1 tool call/)).toBeDefined();
    fireEvent.click(screen.getByRole("button", { name: "Clear session" }));
    expect(screen.getByText("Turn product signals into a clear next move.")).toBeDefined();
  });

  it("redirects expired sessions without exposing an answer error", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(Response.json({
      title: "Session required",
      status: 401,
    }, { status: 401 })));
    render(<CopilotChat />);

    fireEvent.change(screen.getByLabelText(/Ask about feedback/), {
      target: { value: "What changed this week?" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Ask Copilot" }));

    await waitFor(() => expect(router.replace).toHaveBeenCalledWith("/login"));
    expect(screen.queryByRole("alert")).toBeNull();
  });

  it("maps provider outages to a retryable generic message", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(Response.json({
      title: "AI provider unavailable",
      detail: "private provider configuration detail",
      status: 503,
    }, { status: 503 })));
    render(<CopilotChat />);

    fireEvent.click(screen.getByRole("button", { name: /^Changes this week/ }));

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Copilot is temporarily unavailable");
    expect(alert).not.toHaveTextContent("private provider configuration detail");
    expect(screen.getByRole("button", { name: "Try this question again" })).toBeDefined();
  });
});
