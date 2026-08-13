import type { CopilotChatResponse, CopilotToolUsage } from "@/types/copilot";

export const copilotMessageMaxLength = 4_000;

const maximumAnswerLength = 8_000;
const maximumModelTurns = 12;
const maximumToolCalls = 24;
const toolNamePattern = /^[a-z][a-z0-9_]{0,63}$/;

export function parseCopilotChatResponse(value: unknown): CopilotChatResponse | null {
  if (!isRecord(value)
    || !isRequiredString(value.answer, maximumAnswerLength)
    || !isIntegerBetween(value.modelTurnCount, 1, maximumModelTurns)
    || !isIntegerBetween(value.toolCallCount, 0, maximumToolCalls)
    || !Array.isArray(value.toolUsages)
    || value.toolUsages.length !== value.toolCallCount) return null;

  const toolUsages = value.toolUsages.map(parseToolUsage);
  if (toolUsages.some((usage) => usage === null)) return null;

  return {
    answer: value.answer.trim(),
    modelTurnCount: value.modelTurnCount,
    toolCallCount: value.toolCallCount,
    toolUsages: toolUsages as CopilotToolUsage[],
  };
}

export function validateCopilotMessage(value: string): string | null {
  if (!value.trim()) return "Ask PulsePilot a product question first.";
  if (value.length > copilotMessageMaxLength) {
    return `Keep the question under ${copilotMessageMaxLength.toLocaleString("en")} characters.`;
  }
  return null;
}

function parseToolUsage(value: unknown): CopilotToolUsage | null {
  if (!isRecord(value)
    || typeof value.toolName !== "string"
    || !toolNamePattern.test(value.toolName)
    || typeof value.succeeded !== "boolean") return null;

  return { toolName: value.toolName, succeeded: value.succeeded };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

function isRequiredString(value: unknown, maximumLength: number): value is string {
  return typeof value === "string"
    && value.trim().length > 0
    && value.length <= maximumLength;
}

function isIntegerBetween(value: unknown, minimum: number, maximum: number): value is number {
  return typeof value === "number"
    && Number.isInteger(value)
    && value >= minimum
    && value <= maximum;
}
