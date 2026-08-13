import { describe, expect, it } from "vitest";

import {
  copilotMessageMaxLength,
  parseCopilotChatResponse,
  validateCopilotMessage,
} from "./parser";

const response = {
  answer: "Payment failures are the fastest-growing issue this week.",
  modelTurnCount: 2,
  toolCallCount: 2,
  toolUsages: [
    { toolName: "get_feedback_statistics", succeeded: true },
    { toolName: "get_trending_issues", succeeded: true },
  ],
};

describe("copilot response parser", () => {
  it("accepts a bounded grounded answer and tool trace", () => {
    expect(parseCopilotChatResponse(response)).toEqual(response);
  });

  it("rejects mismatched tool counts and malformed tool names", () => {
    expect(parseCopilotChatResponse({ ...response, toolCallCount: 1 })).toBeNull();
    expect(parseCopilotChatResponse({
      ...response,
      toolUsages: [{ toolName: "Get Statistics", succeeded: true }],
      toolCallCount: 1,
    })).toBeNull();
  });

  it("rejects missing answers and orchestration counts outside the contract", () => {
    expect(parseCopilotChatResponse({ ...response, answer: "   " })).toBeNull();
    expect(parseCopilotChatResponse({ ...response, modelTurnCount: 13 })).toBeNull();
    expect(parseCopilotChatResponse({ ...response, toolCallCount: -1, toolUsages: [] }))
      .toBeNull();
  });
});

describe("copilot question validation", () => {
  it("accepts a useful question", () => {
    expect(validateCopilotMessage("What changed this week?")).toBeNull();
  });

  it("rejects blank and oversized questions", () => {
    expect(validateCopilotMessage("   ")).toBe("Ask PulsePilot a product question first.");
    expect(validateCopilotMessage("x".repeat(copilotMessageMaxLength + 1)))
      .toContain("4,000");
  });
});
