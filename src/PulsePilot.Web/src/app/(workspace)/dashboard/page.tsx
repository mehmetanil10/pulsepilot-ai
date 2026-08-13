import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";

import { DashboardView } from "@/components/dashboard/dashboard-view";
import { requireUser } from "@/lib/auth/session";
import { getDashboardData } from "@/lib/dashboard/data";

export const metadata: Metadata = { title: "Dashboard" };

const supportedPeriods = new Set([7, 30, 90]);

export default async function DashboardPage({ searchParams }: PageProps<"/dashboard">) {
  const rawPeriod = (await searchParams).periodDays;
  const parsedPeriod = typeof rawPeriod === "string" ? Number(rawPeriod) : 7;
  const periodDays = supportedPeriods.has(parsedPeriod) ? parsedPeriod : 7;
  const [user, result] = await Promise.all([
    requireUser(),
    getDashboardData(periodDays),
  ]);

  if (!result.ok) {
    if (result.status === 401) redirect("/login");
    return (
      <main className="dashboard-page dashboard-unavailable">
        <p className="eyebrow">Signal overview</p>
        <h1>Dashboard data is temporarily unavailable.</h1>
        <p>
          PulsePilot could not load the workspace snapshot. The underlying API
          may still be starting or briefly unavailable.
        </p>
        <Link className="primary-button" href={`/dashboard?periodDays=${periodDays}`}>
          Try again
        </Link>
      </main>
    );
  }

  return <DashboardView user={user} data={result.data} periodDays={periodDays} />;
}
