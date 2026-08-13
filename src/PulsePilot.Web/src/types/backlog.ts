export type BacklogItemStatus = "open" | "inProgress" | "resolved" | "closed";

export type BacklogItemPriority = "p1" | "p2" | "p3" | "p4";

export type BacklogStatusFilter = BacklogItemStatus | "all";

export type BacklogPriorityFilter = BacklogItemPriority | "all";

export type BacklogItem = {
  id: string;
  sourceClusterId: string;
  sourcePendingActionId: string;
  createdByUserId: string;
  title: string;
  description: string;
  priority: BacklogItemPriority;
  status: BacklogItemStatus;
  createdAt: string;
  updatedAt: string;
};

export type BacklogListPage = {
  items: BacklogItem[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export type BacklogFilters = {
  page: number;
  status: BacklogStatusFilter;
  priority: BacklogPriorityFilter;
  sourcePendingActionId?: string;
};
