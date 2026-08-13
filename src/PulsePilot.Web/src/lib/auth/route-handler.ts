import "server-only";

import type { AuthMode } from "@/lib/auth/validation";
import { validateAuthPayload } from "@/lib/auth/validation";
import {
  authenticateWithBackend,
  parseAuthenticationResponse,
} from "@/lib/auth/backend-auth";
import { setSessionToken } from "@/lib/auth/session";
import { normalizeProblem, problemResponse } from "@/lib/http/problem";
import { hasTrustedOrigin } from "@/lib/http/security";

const MAX_AUTH_BODY_BYTES = 16_384;

export async function handleAuthentication(request: Request, mode: AuthMode): Promise<Response> {
  if (!hasTrustedOrigin(request)) {
    return problemResponse(403, undefined, "İstek kaynağı doğrulanamadı.");
  }

  const contentType = request.headers.get("content-type")?.split(";", 1)[0].trim();
  if (contentType !== "application/json") {
    return problemResponse(415);
  }

  const contentLength = Number(request.headers.get("content-length") ?? 0);
  if (contentLength > MAX_AUTH_BODY_BYTES) {
    return problemResponse(413);
  }

  let body: unknown;
  try {
    const text = await request.text();
    if (new TextEncoder().encode(text).byteLength > MAX_AUTH_BODY_BYTES) {
      return problemResponse(413);
    }
    body = JSON.parse(text);
  } catch {
    return problemResponse(400, undefined, "Geçerli bir JSON gövdesi gönderin.");
  }

  const validation = validateAuthPayload(mode, body);
  if (!validation.success) {
    return problemResponse(400, undefined, validation.detail);
  }

  try {
    const backendResponse = await authenticateWithBackend(mode, validation.data);
    if (!backendResponse.ok) {
      let backendProblem: unknown;
      try {
        backendProblem = await backendResponse.json();
      } catch {
        backendProblem = undefined;
      }

      return Response.json(normalizeProblem(backendProblem, backendResponse.status), {
        status: backendResponse.status,
        headers: { "Cache-Control": "no-store" },
      });
    }

    const authentication = await parseAuthenticationResponse(backendResponse);
    if (!authentication) {
      return problemResponse(502);
    }

    await setSessionToken(authentication.accessToken, authentication.expiresAt);

    return Response.json(
      {
        user: {
          userId: authentication.userId,
          email: authentication.email,
          displayName: authentication.displayName,
          workspaceId: authentication.workspaceId,
          workspaceName: authentication.workspaceName,
          role: authentication.role,
        },
        expiresAt: authentication.expiresAt,
      },
      { status: mode === "register" ? 201 : 200, headers: { "Cache-Control": "no-store" } },
    );
  } catch {
    return problemResponse(503);
  }
}
