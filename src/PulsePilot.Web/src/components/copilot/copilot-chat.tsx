"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";

import { Icon } from "@/components/icons";
import {
  copilotMessageMaxLength,
  parseCopilotChatResponse,
  validateCopilotMessage,
} from "@/lib/copilot/parser";
import { readProblem, type PublicProblem } from "@/lib/http/problem";
import type { CopilotChatResponse } from "@/types/copilot";

type CopilotExchange = {
  id: number;
  question: string;
  response: CopilotChatResponse | null;
  error: string | null;
};

const promptSuggestions = [
  {
    icon: "trend" as const,
    label: "Changes this week",
    prompt: "What changed in customer feedback this week?",
  },
  {
    icon: "feedback" as const,
    label: "Customer complaints",
    prompt: "What are customers complaining about most?",
  },
  {
    icon: "backlog" as const,
    label: "Engineering priorities",
    prompt: "What should engineering prioritize next?",
  },
  {
    icon: "dashboard" as const,
    label: "Weekly report",
    prompt: "Generate a weekly product intelligence report.",
  },
];

const capabilities = [
  { icon: "dashboard" as const, title: "Workspace statistics", copy: "Ground answers in aggregate product signals." },
  { icon: "trend" as const, title: "Trend detection", copy: "Compare current and previous feedback windows." },
  { icon: "feedback" as const, title: "Feedback discovery", copy: "Find semantically related customer evidence." },
  { icon: "spark" as const, title: "Product reports", copy: "Generate grounded insights and priorities." },
];

export function CopilotChat() {
  const router = useRouter();
  const [input, setInput] = useState("");
  const [exchanges, setExchanges] = useState<CopilotExchange[]>([]);
  const [pending, setPending] = useState(false);
  const [fieldError, setFieldError] = useState<string | null>(null);
  const nextId = useRef(1);
  const conversation = useRef<HTMLDivElement>(null);
  const conversationEnd = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (exchanges.length === 0) {
      conversation.current?.scrollTo({ top: 0 });
      return;
    }
    conversationEnd.current?.scrollIntoView({ behavior: "smooth", block: "nearest" });
  }, [exchanges, pending]);

  async function ask(questionValue: string) {
    if (pending) return;
    const validationError = validateCopilotMessage(questionValue);
    if (validationError) {
      setFieldError(validationError);
      return;
    }

    const question = questionValue.trim();
    const id = nextId.current++;
    setFieldError(null);
    setInput("");
    setPending(true);
    setExchanges((current) => [
      ...current,
      { id, question, response: null, error: null },
    ]);

    try {
      const response = await fetch("/api/backend/copilot/chat", {
        method: "POST",
        headers: { Accept: "application/json", "Content-Type": "application/json" },
        body: JSON.stringify({ message: question }),
      });

      if (!response.ok) {
        const problem = await readProblem(response);
        if (problem.status === 401) {
          router.replace("/login");
          return;
        }
        setExchangeError(id, problemMessage(problem));
        return;
      }

      const answer = parseCopilotChatResponse(await response.json());
      if (!answer) {
        setExchangeError(id, "PulsePilot returned an answer outside the expected safety contract. Try again.");
        return;
      }

      setExchanges((current) => current.map((exchange) => (
        exchange.id === id ? { ...exchange, response: answer } : exchange
      )));
    } catch {
      setExchangeError(id, "Copilot could not reach PulsePilot. Check the connection and try again.");
    } finally {
      setPending(false);
    }
  }

  function setExchangeError(id: number, error: string) {
    setExchanges((current) => current.map((exchange) => (
      exchange.id === id ? { ...exchange, error } : exchange
    )));
  }

  return (
    <main className="copilot-page">
      <header className="copilot-page-header">
        <div>
          <p className="eyebrow">Agentic product intelligence</p>
          <h1>Workspace Copilot</h1>
          <p>Ask product questions. PulsePilot selects bounded tools and grounds its answer in your workspace.</p>
        </div>
        <span className="copilot-grounded-badge"><i />Grounded in workspace data</span>
      </header>

      <div className="copilot-layout">
        <aside className="copilot-capabilities">
          <div className="copilot-aside-heading">
            <span><Icon name="copilot" /></span>
            <div><strong>Available capabilities</strong><small>Read-only and analytical</small></div>
          </div>
          <ul>
            {capabilities.map((capability) => (
              <li key={capability.title}>
                <span><Icon name={capability.icon} /></span>
                <div><strong>{capability.title}</strong><small>{capability.copy}</small></div>
              </li>
            ))}
          </ul>
          <section className="copilot-safety-note">
            <Icon name="actions" />
            <div>
              <strong>Human control stays on</strong>
              <p>Copilot can analyze and recommend. It cannot approve actions or change backlog state.</p>
            </div>
          </section>
        </aside>

        <section className="copilot-chat-panel" aria-label="Workspace Copilot conversation">
          <header className="copilot-chat-header">
            <div>
              <span><Icon name="spark" /></span>
              <p><strong>PulsePilot Agent</strong><small>Ready to investigate product signals</small></p>
            </div>
            {exchanges.length > 0 && (
              <button type="button" disabled={pending} onClick={() => setExchanges([])}>
                Clear session
              </button>
            )}
          </header>

          <div className="copilot-conversation" aria-live="polite" ref={conversation}>
            {exchanges.length === 0 ? (
              <CopilotWelcome onAsk={ask} pending={pending} />
            ) : (
              exchanges.map((exchange) => (
                <CopilotExchangeView
                  exchange={exchange}
                  isPending={pending && exchange.id === exchanges.at(-1)?.id}
                  onRetry={() => ask(exchange.question)}
                  key={exchange.id}
                />
              ))
            )}
            <div ref={conversationEnd} />
          </div>

          <form
            className="copilot-composer"
            onSubmit={(event) => { event.preventDefault(); void ask(input); }}
          >
            <label htmlFor="copilot-message">Ask about feedback, trends, priorities, or reports</label>
            <div>
              <textarea
                id="copilot-message"
                name="message"
                value={input}
                onChange={(event) => { setInput(event.target.value); setFieldError(null); }}
                onKeyDown={(event) => {
                  if (event.key === "Enter" && !event.shiftKey && !event.nativeEvent.isComposing) {
                    event.preventDefault();
                    void ask(input);
                  }
                }}
                placeholder="What should engineering prioritize this week?"
                maxLength={copilotMessageMaxLength}
                disabled={pending}
                rows={3}
              />
              <button type="submit" disabled={pending || !input.trim()} aria-label="Ask Copilot">
                {pending ? <i /> : <Icon name="arrow" />}
              </button>
            </div>
            <footer>
              <span className={fieldError ? "error" : undefined}>{fieldError ?? "Independent questions · Enter to send · Shift + Enter for a new line"}</span>
              <small>{input.length.toLocaleString("en")} / {copilotMessageMaxLength.toLocaleString("en")}</small>
            </footer>
          </form>
        </section>
      </div>
    </main>
  );
}

function CopilotWelcome({ onAsk, pending }: { onAsk: (prompt: string) => Promise<void>; pending: boolean }) {
  return (
    <div className="copilot-welcome">
      <span className="copilot-welcome-mark"><Icon name="copilot" /></span>
      <p className="eyebrow">Ask your workspace</p>
      <h2>Turn product signals into a clear next move.</h2>
      <p>Choose a starting point or ask a focused question in your own words.</p>
      <div className="copilot-prompt-grid">
        {promptSuggestions.map((suggestion) => (
          <button
            type="button"
            disabled={pending}
            onClick={() => void onAsk(suggestion.prompt)}
            key={suggestion.label}
          >
            <span><Icon name={suggestion.icon} /></span>
            <p><strong>{suggestion.label}</strong><small>{suggestion.prompt}</small></p>
            <Icon name="arrow" />
          </button>
        ))}
      </div>
    </div>
  );
}

function CopilotExchangeView({
  exchange,
  isPending,
  onRetry,
}: {
  exchange: CopilotExchange;
  isPending: boolean;
  onRetry: () => void;
}) {
  return (
    <article className="copilot-exchange">
      <div className="copilot-user-message">
        <span>You</span>
        <p>{exchange.question}</p>
      </div>
      <div className="copilot-agent-message">
        <span><Icon name="copilot" /></span>
        {isPending ? (
          <div className="copilot-thinking">
            <strong>Investigating workspace signals</strong>
            <p>Choosing the smallest set of grounded tools for this question.</p>
            <span><i /><i /><i /></span>
          </div>
        ) : exchange.error ? (
          <div className="copilot-answer-error" role="alert">
            <strong>Copilot could not complete this answer.</strong>
            <p>{exchange.error}</p>
            <button type="button" onClick={onRetry}>Try this question again</button>
          </div>
        ) : exchange.response ? (
          <div className="copilot-answer">
            <p>{exchange.response.answer}</p>
            <footer>
              <div>
                {exchange.response.toolUsages.length > 0 ? (
                  exchange.response.toolUsages.map((usage, index) => (
                    <span className={usage.succeeded ? "succeeded" : "failed"} key={`${usage.toolName}-${index}`}>
                      <i />{toolLabel(usage.toolName)}
                    </span>
                  ))
                ) : <span className="direct"><i />Direct answer</span>}
              </div>
              <small>{exchange.response.modelTurnCount} model {exchange.response.modelTurnCount === 1 ? "turn" : "turns"} · {exchange.response.toolCallCount} tool {exchange.response.toolCallCount === 1 ? "call" : "calls"}</small>
            </footer>
          </div>
        ) : null}
      </div>
    </article>
  );
}

function toolLabel(toolName: string): string {
  const labels: Record<string, string> = {
    get_feedback_statistics: "Feedback statistics",
    get_trending_issues: "Trending issues",
    search_similar_feedback: "Similar feedback",
    generate_report: "Product report",
  };
  return labels[toolName] ?? toolName.replaceAll("_", " ");
}

function problemMessage(problem: PublicProblem): string {
  if (problem.status === 400) {
    return Object.values(problem.errors ?? {}).flat()[0]
      ?? problem.detail
      ?? "The question did not meet the Copilot input contract.";
  }
  if (problem.status === 429) return "Copilot is handling too many requests. Wait a moment and try again.";
  if (problem.status === 502) return "The AI provider returned an invalid answer. Try the question again.";
  if (problem.status === 503) return "Copilot is temporarily unavailable. Your question was not lost; try again shortly.";
  return problem.detail ?? "The answer could not be completed. Try again.";
}
