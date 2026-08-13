export type ActionStatusFilter =
  | "pending"
  | "approved"
  | "rejected"
  | "executed"
  | "failed"
  | "all";

export type PendingActionContext = {
  priority: string | null;
  priorityScore: number | null;
  category: string | null;
  component: string | null;
  feedbackCount: number | null;
  suggestedAction: string | null;
};

export type PendingActionItem = {
  id: string;
  feedbackId: string;
  feedbackClusterId: string;
  actionType: string;
  title: string;
  description: string;
  status: string;
  approvedAt: string | null;
  rejectedAt: string | null;
  executedAt: string | null;
  createdAt: string;
  updatedAt: string;
  context: PendingActionContext;
};

export type PendingActionListPage = {
  items: PendingActionItem[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export type PendingActionFilters = {
  page: number;
  status: ActionStatusFilter;
};
