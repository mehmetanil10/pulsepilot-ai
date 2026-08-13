export default function DashboardLoading() {
  return (
    <main className="dashboard-page dashboard-skeleton" aria-label="Loading dashboard">
      <div className="skeleton-heading" />
      <div className="skeleton-kpis">
        {[0, 1, 2, 3].map((item) => <span key={item} />)}
      </div>
      <div className="skeleton-panels"><span /><span /></div>
    </main>
  );
}
