import { redirect } from "next/navigation";

import { getVerifiedUser } from "@/lib/auth/session";

export default async function HomePage() {
  redirect((await getVerifiedUser()) ? "/dashboard" : "/login");
}
