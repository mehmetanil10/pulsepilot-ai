import Link from "next/link";

import { Icon } from "@/components/icons";

export default function FeedbackNotFound() {
  return (
    <main className="feedback-page feedback-detail-not-found">
      <span><Icon name="feedback" /></span>
      <p className="eyebrow">Signal not found</p>
      <h1>This feedback is no longer on the radar.</h1>
      <p>It may have been removed, or it may belong to another workspace.</p>
      <Link className="primary-button" href="/feedback">Return to feedback</Link>
    </main>
  );
}
