import "server-only";

import type { AuthMode } from "@/lib/auth/validation";
import type { BackendAuthenticationResponse } from "@/types/auth";
import { getApiBaseUrl } from "@/lib/env";

export async function authenticateWithBackend(
  mode: AuthMode,
  payload: unknown,
): Promise<Response> {
  return fetch(new URL(`api/auth/${mode}`, getApiBaseUrl()), {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
    },
    body: JSON.stringify(payload),
    cache: "no-store",
    signal: AbortSignal.timeout(15_000),
  });
}

export async function parseAuthenticationResponse(
  response: Response,
): Promise<BackendAuthenticationResponse | null> {
  try {
    const value = (await response.json()) as Partial<BackendAuthenticationResponse>;
    if (
      typeof value.accessToken !== "string" ||
      typeof value.tokenType !== "string" ||
      value.tokenType.toLowerCase() !== "bearer" ||
      typeof value.expiresAt !== "string" ||
      typeof value.userId !== "string" ||
      typeof value.email !== "string" ||
      typeof value.displayName !== "string" ||
      typeof value.workspaceId !== "string" ||
      typeof value.workspaceName !== "string" ||
      typeof value.role !== "string"
    ) {
      return null;
    }

    return value as BackendAuthenticationResponse;
  } catch {
    return null;
  }
}
