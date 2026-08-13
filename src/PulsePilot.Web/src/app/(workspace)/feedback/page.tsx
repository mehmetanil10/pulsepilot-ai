import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";

import { FeedbackListView } from "@/components/feedback/feedback-list-view";
import { getFeedbackList } from "@/lib/feedback/data";
import { feedbackHref, parseFeedbackFilters } from "@/lib/feedback/query";

export const metadata: Metadata = { title: "Feedback" };

export default async function FeedbackPage({ searchParams }: PageProps<"/feedback">) {
  const filters = parseFeedbackFilters(await searchParams);
  const result = await getFeedbackList(filters);

  if (!result.ok) {
    if (result.status === 401) redirect("/login");
    return (
      <main className="feedback-page feedback-unavailable">
        <p className="eyebrow">Customer signal library</p>
        <h1>Feedback is temporarily unavailable.</h1>
        <p>The workspace signal stream could not be loaded. Try again in a moment.</p>
        <Link className="primary-button" href={feedbackHref(filters)}>Try again</Link>
      </main>
    );
  }

  const totalPages = Math.max(1, Math.ceil(result.data.totalCount / result.data.pageSize));
  if (filters.page > totalPages) {
    redirect(feedbackHref(filters, { page: totalPages }));
  }

  return <FeedbackListView filters={filters} page={result.data} />;
}
