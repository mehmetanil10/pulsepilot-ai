import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  authenticateWithBackend: vi.fn(),
  parseAuthenticationResponse: vi.fn(),
  setSessionToken: vi.fn(),
}));

vi.mock("server-only", () => ({}));
vi.mock("@/lib/auth/backend-auth", () => ({
  authenticateWithBackend: mocks.authenticateWithBackend,
  parseAuthenticationResponse: mocks.parseAuthenticationResponse,
}));
vi.mock("@/lib/auth/session", () => ({
  setSessionToken: mocks.setSessionToken,
}));

import { handleAuthentication } from "./route-handler";

const authentication = {
  accessToken: "access-token",
  tokenType: "Bearer",
  expiresAt: "2099-01-01T00:00:00Z",
  userId: "user-id",
  email: "owner@example.com",
  displayName: "Workspace Owner",
  workspaceId: "workspace-id",
  workspaceName: "Product Team",
  role: "Admin",
};

describe("authentication route handler", () => {
  beforeEach(() => {
    mocks.authenticateWithBackend.mockResolvedValue(Response.json(authentication));
    mocks.parseAuthenticationResponse.mockResolvedValue(authentication);
    mocks.setSessionToken.mockResolvedValue(undefined);
  });

  it("creates a server-side session without returning the access token", async () => {
    const response = await handleAuthentication(authRequest({
      email: "OWNER@EXAMPLE.COM",
      password: "correct-horse-battery-staple",
      displayName: "Workspace Owner",
      workspaceName: "Product Team",
    }), "register");
    const body = await response.json();

    expect(response.status).toBe(201);
    expect(mocks.authenticateWithBackend).toHaveBeenCalledWith("register", {
      email: "owner@example.com",
      password: "correct-horse-battery-staple",
      displayName: "Workspace Owner",
      workspaceName: "Product Team",
    });
    expect(mocks.setSessionToken).toHaveBeenCalledWith(
      authentication.accessToken,
      authentication.expiresAt,
    );
    expect(body.user).toMatchObject({ email: "owner@example.com", role: "Admin" });
    expect(JSON.stringify(body)).not.toContain(authentication.accessToken);
  });

  it("rejects untrusted, unsupported, oversized, malformed, and invalid requests", async () => {
    const crossOrigin = await handleAuthentication(authRequest(
      { email: "owner@example.com", password: "password" },
      { origin: "https://attacker.example" },
    ), "login");
    const unsupported = await handleAuthentication(new Request(
      "http://localhost:3000/api/auth/login",
      {
        method: "POST",
        headers: { Origin: "http://localhost:3000", "Content-Type": "text/plain" },
        body: "owner@example.com",
      },
    ), "login");
    const oversized = await handleAuthentication(authRequest(
      { email: "owner@example.com", password: "password" },
      { "content-length": "16385" },
    ), "login");
    const malformed = await handleAuthentication(new Request(
      "http://localhost:3000/api/auth/login",
      {
        method: "POST",
        headers: { Origin: "http://localhost:3000", "Content-Type": "application/json" },
        body: "{",
      },
    ), "login");
    const invalid = await handleAuthentication(authRequest({
      email: "not-an-email",
      password: "",
    }), "login");

    expect(crossOrigin.status).toBe(403);
    expect(unsupported.status).toBe(415);
    expect(oversized.status).toBe(413);
    expect(malformed.status).toBe(400);
    expect(invalid.status).toBe(400);
    expect(mocks.authenticateWithBackend).not.toHaveBeenCalled();
  });

  it("normalizes backend problems and removes private fields", async () => {
    mocks.authenticateWithBackend.mockResolvedValue(Response.json({
      title: "Authentication failed",
      status: 401,
      detail: "Credentials were rejected.",
      internalException: "DatabaseException",
    }, { status: 401 }));

    const response = await handleAuthentication(authRequest({
      email: "owner@example.com",
      password: "wrong-password",
    }), "login");
    const text = await response.text();

    expect(response.status).toBe(401);
    expect(text).toContain("Authentication failed");
    expect(text).not.toContain("DatabaseException");
  });

  it("fails closed for malformed success responses and transport failures", async () => {
    mocks.parseAuthenticationResponse.mockResolvedValueOnce(null);
    const malformedSuccess = await handleAuthentication(authRequest({
      email: "owner@example.com",
      password: "password",
    }), "login");

    mocks.authenticateWithBackend.mockRejectedValueOnce(new Error("private transport error"));
    const unavailable = await handleAuthentication(authRequest({
      email: "owner@example.com",
      password: "password",
    }), "login");

    expect(malformedSuccess.status).toBe(502);
    expect(unavailable.status).toBe(503);
  });
});

function authRequest(
  body: unknown,
  headers: Record<string, string> = {},
): Request {
  return new Request("http://localhost:3000/api/auth/login", {
    method: "POST",
    headers: {
      Origin: "http://localhost:3000",
      "Content-Type": "application/json",
      ...headers,
    },
    body: JSON.stringify(body),
  });
}
