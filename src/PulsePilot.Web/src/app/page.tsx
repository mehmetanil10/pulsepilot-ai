import type { Metadata } from "next";

import { LandingPage } from "@/components/landing/landing-page";
import { getVerifiedUser } from "@/lib/auth/session";

export const metadata: Metadata = {
  title: "AI Product Feedback & Engineering Copilot",
  description:
    "Turn fragmented customer feedback into structured product intelligence, human-reviewed actions, and an evidence-backed engineering backlog.",
  keywords: [
    "product feedback analytics",
    "AI product copilot",
    "voice of customer",
    "engineering backlog",
    "human in the loop AI",
  ],
  openGraph: {
    title: "PulsePilot — From customer signals to engineering action",
    description:
      "An AI-driven product feedback and engineering copilot built around accountable, human-reviewed workflows.",
    type: "website",
  },
  twitter: {
    card: "summary_large_image",
    title: "PulsePilot — AI Product Feedback Copilot",
    description: "Structured insight, accountable action, and traceable product decisions.",
  },
};

export default async function HomePage() {
  const user = await getVerifiedUser();

  return <LandingPage authenticated={Boolean(user)} />;
}
