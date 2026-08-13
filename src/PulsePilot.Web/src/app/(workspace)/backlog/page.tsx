import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";

import { BacklogView } from "@/components/backlog/backlog-view";
import { getBacklogList } from "@/lib/backlog/data";
import { backlogHref, parseBacklogFilters } from "@/lib/backlog/query";

export const metadata: Metadata = { title: "Engineering backlog" };

export default async function BacklogPage({ searchParams }: PageProps<"/backlog">) {
  const filters = parseBacklogFilters(await searchParams);
  const result = await getBacklogList(filters);

  if (!result.ok) {
    if (result.status === 401) redirect("/login");
    return (
      <main className="backlog-page backlog-unavailable">
        <p className="eyebrow">Product-to-engineering trace</p>
        <h1>The engineering backlog is temporarily unavailable.</h1>
        <p>No work item was changed. Try loading the workspace backlog again in a moment.</p>
        <Link className="primary-button" href={backlogHref(filters)}>Try again</Link>
      </main>
    );
  }

  const totalPages = Math.max(1, Math.ceil(result.data.totalCount / result.data.pageSize));
  if (filters.page > totalPages) {
    redirect(backlogHref(filters, { page: totalPages }));
  }

  return <BacklogView filters={filters} page={result.data} />;
}
