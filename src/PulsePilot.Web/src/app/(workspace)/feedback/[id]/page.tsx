import type { Metadata } from "next";
import Link from "next/link";
import { notFound, redirect } from "next/navigation";

import { FeedbackDetailView } from "@/components/feedback/feedback-detail-view";
import { getFeedbackDetail } from "@/lib/feedback/detail-data";

export const metadata: Metadata = { title: "Feedback detail" };

const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export default async function FeedbackDetailPage({ params }: PageProps<"/feedback/[id]">) {
  const { id } = await params;
  if (!guidPattern.test(id)) notFound();

  const result = await getFeedbackDetail(id);
  if (!result.ok) {
    if (result.status === 401) redirect("/login");
    if (result.status === 404) notFound();

    return (
      <main className="feedback-page feedback-unavailable">
        <p className="eyebrow">Signal intelligence</p>
        <h1>This feedback is temporarily unavailable.</h1>
        <p>The signal detail could not be loaded. No data was changed; try again in a moment.</p>
        <div className="detail-error-actions">
          <Link className="primary-button" href={`/feedback/${id}`}>Try again</Link>
          <Link href="/feedback">Return to feedback</Link>
        </div>
      </main>
    );
  }

  return <FeedbackDetailView bundle={result.data} />;
}
