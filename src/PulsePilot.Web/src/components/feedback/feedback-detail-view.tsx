import Link from "next/link";

import { Icon } from "@/components/icons";
import {
  displayFeedbackValue,
  formatFeedbackDateTime,
} from "@/lib/feedback/presentation";
import type {
  FeedbackAnalysis,
  FeedbackDetailBundle,
  SimilarFeedbackItem,
} from "@/types/feedback";

export function FeedbackDetailView({ bundle }: { bundle: FeedbackDetailBundle }) {
  const { feedback } = bundle;

  return (
    <main className="feedback-detail-page">
      <Link className="detail-back-link" href="/feedback">
        <Icon name="arrow" /> All feedback
      </Link>

      <header className="feedback-detail-header">
        <div>
          <p className="eyebrow">Signal intelligence</p>
          <h1>{feedback.title || "Untitled feedback"}</h1>
          <p>Original customer context, AI interpretation, and related product signals.</p>
        </div>
        <span className={`feedback-status-chip ${feedback.processingStatus}`}>
          <i aria-hidden="true" />
          {displayFeedbackValue(feedback.processingStatus)}
        </span>
      </header>

      <div className="feedback-detail-layout">
        <div className="feedback-detail-main">
          <section className="detail-card original-feedback-card" aria-labelledby="original-feedback-heading">
            <DetailCardHeading
              eyebrow="Original signal"
              heading="Original feedback"
              aside={displayFeedbackValue(feedback.source)}
              id="original-feedback-heading"
            />
            <blockquote>{feedback.content}</blockquote>
            <footer>
              <span>Received {formatFeedbackDateTime(feedback.createdAt)}</span>
              {feedback.updatedAt !== feedback.createdAt && (
                <span>Updated {formatFeedbackDateTime(feedback.updatedAt)}</span>
              )}
            </footer>
          </section>

          <AnalysisPanel
            analysis={bundle.analysis}
            state={bundle.analysisState}
            processingStatus={feedback.processingStatus}
          />
        </div>

        <aside className="feedback-detail-aside">
          <ProcessingPanel bundle={bundle} />
          <ClusterPanel bundle={bundle} />
        </aside>
      </div>

      <SimilarFeedbackPanel bundle={bundle} />
    </main>
  );
}

function DetailCardHeading({
  eyebrow,
  heading,
  aside,
  id,
}: {
  eyebrow: string;
  heading: string;
  aside?: string;
  id: string;
}) {
  return (
    <header className="detail-card-heading">
      <div>
        <p className="eyebrow">{eyebrow}</p>
        <h2 id={id}>{heading}</h2>
      </div>
      {aside && <span>{aside}</span>}
    </header>
  );
}

function AnalysisPanel({
  analysis,
  state,
  processingStatus,
}: {
  analysis: FeedbackAnalysis | null;
  state: FeedbackDetailBundle["analysisState"];
  processingStatus: string;
}) {
  if (state === "unavailable") {
    return (
      <section className="detail-card analysis-card" aria-labelledby="analysis-heading">
        <DetailCardHeading eyebrow="AI interpretation" heading="Analysis" id="analysis-heading" />
        <DetailEmptyState
          icon="alert"
          title="Analysis could not be loaded."
          copy="The original feedback is still available. Try this page again in a moment."
        />
      </section>
    );
  }

  if (!analysis?.analysis) {
    const failed = processingStatus === "failed";
    return (
      <section className="detail-card analysis-card" aria-labelledby="analysis-heading">
        <DetailCardHeading eyebrow="AI interpretation" heading="Analysis" id="analysis-heading" />
        <DetailEmptyState
          icon={failed ? "alert" : "spark"}
          title={failed ? "Processing did not complete." : "Analysis is being prepared."}
          copy={failed
            ? "The signal is preserved and can be queued for another processing attempt."
            : "Category, severity, sentiment, and recommended action will appear here when the AI pipeline completes."}
        />
      </section>
    );
  }

  const result = analysis.analysis;
  return (
    <section className="detail-card analysis-card" aria-labelledby="analysis-heading">
      <DetailCardHeading
        eyebrow="AI interpretation"
        heading="Analysis"
        aside={`${Math.round(result.confidence * 100)}% confidence`}
        id="analysis-heading"
      />

      {!analysis.isCurrent && (
        <div className="analysis-stale-notice">
          <Icon name="clock" />
          <span><strong>Previous analysis</strong>This signal changed and is being analyzed again.</span>
        </div>
      )}

      <div className="analysis-summary">
        <span><Icon name="spark" /></span>
        <div>
          <small>AI summary</small>
          <p>{result.summary}</p>
        </div>
      </div>

      <dl className="analysis-metrics">
        <AnalysisMetric label="Category" value={displayFeedbackValue(result.category)} />
        <AnalysisMetric label="Component" value={displayFeedbackValue(result.component)} />
        <AnalysisMetric label="Severity" value={`${result.severity} / 5`} tone={`severity-${result.severity}`} />
        <AnalysisMetric label="Sentiment" value={displayFeedbackValue(result.sentiment)} tone={result.sentiment} />
        <AnalysisMetric label="Confidence" value={`${Math.round(result.confidence * 100)}%`} />
      </dl>

      <div className="suggested-action">
        <span><Icon name="arrow" /></span>
        <div>
          <small>Suggested action</small>
          <p>{result.suggestedAction}</p>
        </div>
      </div>
    </section>
  );
}

function AnalysisMetric({ label, value, tone }: { label: string; value: string; tone?: string }) {
  return (
    <div>
      <dt>{label}</dt>
      <dd className={tone}>{value}</dd>
    </div>
  );
}

function ProcessingPanel({ bundle }: { bundle: FeedbackDetailBundle }) {
  const { feedback, analysis } = bundle;
  const status = displayFeedbackValue(feedback.processingStatus);

  return (
    <section className="detail-card processing-card" aria-labelledby="processing-heading">
      <DetailCardHeading eyebrow="AI pipeline" heading="Processing" id="processing-heading" />
      <div className={`processing-status ${feedback.processingStatus}`}>
        <span><Icon name={feedback.processingStatus === "failed" ? "alert" : "spark"} /></span>
        <div>
          <small>Current state</small>
          <strong>{status}</strong>
        </div>
      </div>
      <p className="processing-description">{processingDescription(feedback.processingStatus)}</p>
      <dl className="processing-facts">
        <div><dt>Received</dt><dd>{formatFeedbackDateTime(feedback.createdAt)}</dd></div>
        <div><dt>Signal updated</dt><dd>{formatFeedbackDateTime(feedback.updatedAt)}</dd></div>
        {analysis?.analysis && (
          <div><dt>Analysis updated</dt><dd>{formatFeedbackDateTime(analysis.analysis.updatedAt)}</dd></div>
        )}
        <div>
          <dt>Analysis version</dt>
          <dd>{analysis?.isCurrent
            ? "Current"
            : analysis?.analysis
              ? "Pending refresh"
              : "Not available"}</dd>
        </div>
      </dl>
    </section>
  );
}

function ClusterPanel({ bundle }: { bundle: FeedbackDetailBundle }) {
  const { cluster, clusterState } = bundle;

  return (
    <section className="detail-card cluster-card" aria-labelledby="cluster-heading">
      <DetailCardHeading eyebrow="Pattern detection" heading="Associated cluster" id="cluster-heading" />
      {clusterState === "ready" && cluster ? (
        <>
          <div className="cluster-priority-row">
            <span className={`priority-chip ${cluster.priority}`}>{cluster.priority.toUpperCase()}</span>
            <strong>{cluster.priorityScore.toFixed(1)}</strong>
            <small>priority score</small>
          </div>
          <h3>{cluster.title}</h3>
          <p>{displayFeedbackValue(cluster.category)} · {displayFeedbackValue(cluster.component)}</p>
          <footer>
            <strong>{cluster.totalFeedbackCount}</strong>
            <span>related signal{cluster.totalFeedbackCount === 1 ? "" : "s"} in this cluster</span>
          </footer>
        </>
      ) : (
        <DetailEmptyState
          icon={clusterState === "unavailable" ? "alert" : "trend"}
          title={clusterState === "unavailable" ? "Cluster could not be loaded." : "No cluster assigned yet."}
          copy={clusterState === "unavailable"
            ? "The relationship data is temporarily unavailable."
            : "A matching product pattern will appear after analysis and similarity processing."}
          compact
        />
      )}
    </section>
  );
}

function SimilarFeedbackPanel({ bundle }: { bundle: FeedbackDetailBundle }) {
  const { similarFeedback, similarState } = bundle;

  return (
    <section className="detail-card similar-feedback-card" aria-labelledby="similar-heading">
      <DetailCardHeading
        eyebrow="Semantic matches"
        heading="Similar feedback"
        aside={similarFeedback ? `${similarFeedback.items.length} matches` : undefined}
        id="similar-heading"
      />

      {similarState === "ready" && similarFeedback && similarFeedback.items.length > 0 ? (
        <div className="similar-feedback-list">
          {similarFeedback.items.map((item) => <SimilarFeedbackRow item={item} key={item.id} />)}
        </div>
      ) : (
        <DetailEmptyState
          icon={similarState === "unavailable" ? "alert" : "feedback"}
          title={similarState === "blocked"
            ? "Similarity processing is not ready."
            : similarState === "unavailable"
              ? "Similar feedback could not be loaded."
              : "No similar signals found."}
          copy={similarState === "blocked"
            ? "Semantic matches will become available when the current analysis and embedding finish."
            : similarState === "unavailable"
              ? "This section can be retried without affecting the original feedback."
              : `No feedback meets the ${Math.round((similarFeedback?.similarityThreshold ?? 0) * 100)}% similarity threshold.`}
        />
      )}
    </section>
  );
}

function SimilarFeedbackRow({ item }: { item: SimilarFeedbackItem }) {
  return (
    <Link className="similar-feedback-row" href={`/feedback/${item.id}`} prefetch={false}>
      <span className="similarity-score">{Math.round(item.similarity * 100)}%</span>
      <div>
        <strong>{item.title || "Untitled feedback"}</strong>
        <p>{item.content}</p>
        <small>{displayFeedbackValue(item.source)} · {formatFeedbackDateTime(item.createdAt)}</small>
      </div>
      <Icon name="arrow" />
    </Link>
  );
}

function DetailEmptyState({
  icon,
  title,
  copy,
  compact = false,
}: {
  icon: "alert" | "feedback" | "spark" | "trend";
  title: string;
  copy: string;
  compact?: boolean;
}) {
  return (
    <div className={`detail-empty-state${compact ? " compact" : ""}`}>
      <span><Icon name={icon} /></span>
      <div><strong>{title}</strong><p>{copy}</p></div>
    </div>
  );
}

function processingDescription(status: string): string {
  switch (status) {
    case "completed": return "Structured analysis and semantic representation are ready.";
    case "processing": return "The worker is analyzing this signal and preparing its semantic context.";
    case "failed": return "The last processing attempt did not complete; the original signal remains safe.";
    default: return "This signal is queued and waiting for an available processing worker.";
  }
}
