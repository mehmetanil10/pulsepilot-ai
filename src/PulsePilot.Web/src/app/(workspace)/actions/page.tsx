import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";

import { PendingActionsView } from "@/components/actions/pending-actions-view";
import { requireUser } from "@/lib/auth/session";
import { getPendingActionList } from "@/lib/actions/data";
import { parsePendingActionFilters, pendingActionHref } from "@/lib/actions/query";

export const metadata: Metadata = { title: "Pending actions" };

export default async function PendingActionsPage({ searchParams }: PageProps<"/actions">) {
  const filters = parsePendingActionFilters(await searchParams);
  const user = await requireUser();
  const result = await getPendingActionList(filters);

  if (!result.ok) {
    if (result.status === 401) redirect("/login");
    return (
      <main className="actions-page actions-unavailable">
        <p className="eyebrow">Human-in-the-loop</p>
        <h1>The action review queue is temporarily unavailable.</h1>
        <p>No recommendation was changed. Try loading the workspace queue again in a moment.</p>
        <Link className="primary-button" href={pendingActionHref(filters)}>Try again</Link>
      </main>
    );
  }

  const totalPages = Math.max(1, Math.ceil(result.data.totalCount / result.data.pageSize));
  if (filters.page > totalPages) {
    redirect(pendingActionHref(filters, { page: totalPages }));
  }

  return (
    <PendingActionsView
      filters={filters}
      page={result.data}
      canReview={user.role.toLowerCase() === "admin"}
    />
  );
}
