export type DashboardKpis = {
  feedbackToday: number;
  aiProcessed: number;
  criticalIssues: number;
  pendingActions: number;
  processingFailures: number;
  averageSeverity: number | null;
};

export type DashboardCategoryCount = {
  category: string;
  count: number;
};

export type DashboardRecentFeedback = {
  id: string;
  title: string | null;
  source: string;
  processingStatus: string;
  createdAt: string;
};

export type DashboardPendingAction = {
  id: string;
  actionType: string;
  title: string;
  description: string;
  createdAt: string;
};

export type DashboardSummary = {
  generatedAt: string;
  periodFromInclusive: string;
  periodDays: number;
  kpis: DashboardKpis;
  categories: DashboardCategoryCount[];
  recentFeedback: DashboardRecentFeedback[];
  pendingActions: DashboardPendingAction[];
};

export type DashboardTrendingIssue = {
  feedbackClusterId: string;
  title: string;
  category: string;
  component: string;
  priority: string;
  priorityScore: number;
  currentPeriodCount: number;
  previousPeriodCount: number;
  deltaCount: number;
  growthPercentage: number | null;
  isNew: boolean;
};

export type DashboardTrending = {
  previousFromInclusive: string;
  currentFromInclusive: string;
  currentToExclusive: string;
  periodDays: number;
  items: DashboardTrendingIssue[];
};

export type DashboardData = {
  summary: DashboardSummary;
  trending: DashboardTrending;
};
