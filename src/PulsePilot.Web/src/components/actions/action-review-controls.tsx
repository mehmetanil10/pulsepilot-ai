"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

import { Icon } from "@/components/icons";
import { readProblem } from "@/lib/http/problem";

type ReviewDecision = "approve" | "reject";

export function ActionReviewControls({
  actionId,
  actionType,
  canReview,
}: {
  actionId: string;
  actionType: string;
  canReview: boolean;
}) {
  const router = useRouter();
  const [decision, setDecision] = useState<ReviewDecision | null>(null);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!canReview) {
    return (
      <div className="action-review-readonly">
        <Icon name="clock" />
        <span><strong>Admin review required</strong>Workspace members can inspect recommendations but cannot decide them.</span>
      </div>
    );
  }

  async function review() {
    if (!decision || pending) return;
    setPending(true);
    setError(null);

    try {
      const response = await fetch(`/api/backend/actions/${actionId}/${decision}`, {
        method: "POST",
        headers: { Accept: "application/json" },
      });

      if (!response.ok) {
        const problem = await readProblem(response);
        if (problem.status === 401) {
          router.replace("/login");
          return;
        }
        if (problem.status === 409) {
          setError("This recommendation was already reviewed. Refreshing its current state…");
          router.refresh();
          return;
        }
        setError(problem.detail ?? problem.title);
        return;
      }

      setDecision(null);
      router.refresh();
    } catch {
      setError("The review could not reach PulsePilot. No decision was recorded.");
    } finally {
      setPending(false);
    }
  }

  if (decision) {
    const approving = decision === "approve";
    return (
      <div className={`action-review-confirmation ${decision}`}>
        <div>
          <strong>{approving ? "Approve this recommendation?" : "Reject this recommendation?"}</strong>
          <p>{approving
            ? approvalEffect(actionType)
            : "The recommendation will be closed without executing its action."}</p>
        </div>
        {error && <p className="action-review-error" role="alert">{error}</p>}
        <div className="action-review-buttons">
          <button type="button" onClick={() => { setDecision(null); setError(null); }} disabled={pending}>Cancel</button>
          <button className={approving ? "approve" : "reject"} type="button" onClick={review} disabled={pending}>
            {pending ? "Recording decision…" : approving ? "Confirm approval" : "Confirm rejection"}
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="action-review-controls">
      <div>
        <strong>Human decision required</strong>
        <span>Nothing runs until a workspace admin approves it.</span>
      </div>
      {error && <p className="action-review-error" role="alert">{error}</p>}
      <div className="action-review-buttons">
        <button className="reject" type="button" onClick={() => setDecision("reject")}>Reject</button>
        <button className="approve" type="button" onClick={() => setDecision("approve")}>
          Approve <Icon name="arrow" />
        </button>
      </div>
    </div>
  );
}

function approvalEffect(actionType: string): string {
  switch (actionType) {
    case "createEngineeringIssue":
      return "PulsePilot will create one backlog item through the controlled backend tool.";
    case "draftCustomerResponse":
      return "PulsePilot will generate an unsent customer-response draft for further human review.";
    case "generateReport":
      return "PulsePilot will generate the approved report through the backend tool.";
    default:
      return "PulsePilot will record the approval; no unlisted external side effect is allowed.";
  }
}
