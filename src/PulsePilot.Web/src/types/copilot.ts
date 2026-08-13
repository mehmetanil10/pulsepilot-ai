export type CopilotToolUsage = {
  toolName: string;
  succeeded: boolean;
};

export type CopilotChatResponse = {
  answer: string;
  modelTurnCount: number;
  toolCallCount: number;
  toolUsages: CopilotToolUsage[];
};
