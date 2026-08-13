export default function FeedbackLoading() {
  return (
    <main className="feedback-page feedback-skeleton" aria-label="Loading feedback">
      <div className="feedback-skeleton-heading" />
      <div className="feedback-skeleton-filters" />
      <div className="feedback-skeleton-list">
        {[0, 1, 2, 3, 4, 5].map((item) => <span key={item} />)}
      </div>
    </main>
  );
}
