export default function PendingActionsLoading() {
  return (
    <main className="actions-page actions-skeleton" aria-label="Loading pending actions">
      <span className="actions-skeleton-heading" />
      <span className="actions-skeleton-workflow" />
      <span className="actions-skeleton-tabs" />
      <div className="actions-skeleton-list"><span /><span /></div>
    </main>
  );
}
