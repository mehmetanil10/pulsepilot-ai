"use client";

import { useEffect } from "react";

export default function ErrorPage({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <main className="status-page">
      <p className="eyebrow">Unexpected turbulence</p>
      <h1>PulsePilot could not render this view.</h1>
      <p>Retry the request. If it continues, check the API and web service health.</p>
      <button className="primary-button" type="button" onClick={reset}>Try again</button>
    </main>
  );
}
