export type FeedbackListItem = {
  id: string;
  feedbackClusterId: string | null;
  title: string | null;
  content: string;
  source: string;
  processingStatus: string;
  createdAt: string;
  updatedAt: string;
  category: string | null;
  component: string | null;
  severity: number | null;
  sentiment: string | null;
};

export type FeedbackListPage = {
  items: FeedbackListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export type FeedbackListFilters = {
  page: number;
  source?: string;
  processingStatus?: string;
  category?: string;
  component?: string;
  severity?: number;
  sentiment?: string;
  dateFrom?: string;
  dateTo?: string;
  search?: string;
};
