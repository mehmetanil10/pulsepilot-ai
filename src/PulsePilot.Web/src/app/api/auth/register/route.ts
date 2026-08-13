import { handleAuthentication } from "@/lib/auth/route-handler";

export async function POST(request: Request): Promise<Response> {
  return handleAuthentication(request, "register");
}
