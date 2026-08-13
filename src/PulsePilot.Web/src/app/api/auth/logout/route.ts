import { deleteSessionToken } from "@/lib/auth/session";
import { problemResponse } from "@/lib/http/problem";
import { hasTrustedOrigin } from "@/lib/http/security";

export async function POST(request: Request): Promise<Response> {
  if (!hasTrustedOrigin(request)) {
    return problemResponse(403, undefined, "İstek kaynağı doğrulanamadı.");
  }

  await deleteSessionToken();
  return new Response(null, { status: 204, headers: { "Cache-Control": "no-store" } });
}
