import Link from "next/link";

import { PulseMark } from "@/components/brand/pulse-mark";

export default function NotFound() {
  return (
    <main className="status-page">
      <PulseMark />
      <p className="eyebrow">404 · Signal lost</p>
      <h1>This route is not on the radar.</h1>
      <p>The workspace is healthy, but the page you requested does not exist.</p>
      <Link className="primary-button" href="/dashboard">Return to dashboard</Link>
    </main>
  );
}
