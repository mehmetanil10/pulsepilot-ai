import "server-only";

import { cache } from "react";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";

import type { BackendCurrentUserResponse } from "@/types/auth";
import { getApiBaseUrl } from "@/lib/env";

export const SESSION_COOKIE =
  process.env.NODE_ENV === "production"
    ? "__Host-pulsepilot_session"
    : "pulsepilot_session";

const sessionCookieOptions = {
  httpOnly: true,
  secure: process.env.NODE_ENV === "production",
  sameSite: "lax" as const,
  path: "/",
  priority: "high" as const,
};

export async function setSessionToken(token: string, expiresAt: string): Promise<void> {
  const expires = new Date(expiresAt);
  if (!token || Number.isNaN(expires.getTime()) || expires <= new Date()) {
    throw new Error("API returned an invalid authentication session.");
  }

  (await cookies()).set(SESSION_COOKIE, token, {
    ...sessionCookieOptions,
    expires,
  });
}

export async function deleteSessionToken(): Promise<void> {
  (await cookies()).set(SESSION_COOKIE, "", {
    ...sessionCookieOptions,
    maxAge: 0,
  });
}

export async function readSessionToken(): Promise<string | undefined> {
  return (await cookies()).get(SESSION_COOKIE)?.value;
}

export const getVerifiedUser = cache(async (): Promise<BackendCurrentUserResponse | null> => {
  const token = await readSessionToken();
  if (!token) {
    return null;
  }

  try {
    const response = await fetch(new URL("api/auth/me", getApiBaseUrl()), {
      headers: {
        Accept: "application/json",
        Authorization: `Bearer ${token}`,
      },
      cache: "no-store",
      signal: AbortSignal.timeout(10_000),
    });

    if (!response.ok) {
      return null;
    }

    const value = (await response.json()) as Partial<BackendCurrentUserResponse>;
    if (
      typeof value.userId !== "string" ||
      typeof value.email !== "string" ||
      typeof value.displayName !== "string" ||
      typeof value.workspaceId !== "string" ||
      typeof value.role !== "string"
    ) {
      return null;
    }

    return value as BackendCurrentUserResponse;
  } catch {
    return null;
  }
});

export async function requireUser(): Promise<BackendCurrentUserResponse> {
  const user = await getVerifiedUser();
  if (!user) {
    redirect("/login");
  }

  return user;
}
