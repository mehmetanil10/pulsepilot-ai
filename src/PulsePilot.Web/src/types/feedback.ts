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

export type FeedbackDetail = {
  id: string;
  feedbackClusterId: string | null;
  title: string | null;
  content: string;
  source: string;
  processingStatus: string;
  createdAt: string;
  updatedAt: string;
};

export type FeedbackAnalysisResult = {
  id: string;
  category: string;
  component: string;
  severity: number;
  sentiment: string;
  summary: string;
  suggestedAction: string;
  confidence: number;
  createdAt: string;
  updatedAt: string;
};

export type FeedbackAnalysis = {
  feedbackId: string;
  processingStatus: string;
  isCurrent: boolean;
  analysis: FeedbackAnalysisResult | null;
};

export type SimilarFeedbackItem = {
  id: string;
  feedbackClusterId: string | null;
  title: string | null;
  content: string;
  source: string;
  similarity: number;
  createdAt: string;
};

export type SimilarFeedback = {
  feedbackId: string;
  similarityThreshold: number;
  items: SimilarFeedbackItem[];
};

export type FeedbackCluster = {
  id: string;
  title: string;
  category: string;
  component: string;
  priorityScore: number;
  priority: string;
  totalFeedbackCount: number;
  createdAt: string;
  updatedAt: string;
};

export type FeedbackDetailBundle = {
  feedback: FeedbackDetail;
  analysis: FeedbackAnalysis | null;
  analysisState: "ready" | "unavailable";
  similarFeedback: SimilarFeedback | null;
  similarState: "ready" | "blocked" | "unavailable";
  cluster: FeedbackCluster | null;
  clusterState: "ready" | "missing" | "unavailable";
};
