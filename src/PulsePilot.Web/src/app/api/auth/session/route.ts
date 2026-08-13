import { getVerifiedUser } from "@/lib/auth/session";
import { problemResponse } from "@/lib/http/problem";

export async function GET(): Promise<Response> {
  const user = await getVerifiedUser();
  if (!user) {
    return problemResponse(401);
  }

  return Response.json({ user }, { headers: { "Cache-Control": "no-store" } });
}
