import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  getApiBaseUrl: vi.fn(),
  readSessionToken: vi.fn(),
}));

vi.mock("@/lib/env", () => ({ getApiBaseUrl: mocks.getApiBaseUrl }));
vi.mock("@/lib/auth/session", () => ({ readSessionToken: mocks.readSessionToken }));

import { GET, POST } from "./route";

describe("authenticated backend gateway", () => {
  beforeEach(() => {
    mocks.getApiBaseUrl.mockReturnValue(new URL("http://api:8080/"));
    mocks.readSessionToken.mockResolvedValue("trusted-session-token");
  });

  it("forwards allowlisted requests with only trusted headers", async () => {
    const backendFetch = vi.fn().mockResolvedValue(Response.json(
      { id: "feedback-id" },
      { status: 201, headers: { "X-Request-Id": "request-1" } },
    ));
    vi.stubGlobal("fetch", backendFetch);

    const response = await POST(new Request(
      "http://localhost:3000/api/backend/feedback?source=manual",
      {
        method: "POST",
        headers: {
          Origin: "http://localhost:3000",
          "Content-Type": "application/json",
          Cookie: "private=cookie",
          "X-Private-Header": "must-not-forward",
        },
        body: JSON.stringify({ title: "Checkout issue" }),
      },
    ), context(["feedback"]));

    expect(response.status).toBe(201);
    expect(response.headers.get("x-request-id")).toBe("request-1");
    const [url, init] = backendFetch.mock.calls[0] as [URL, RequestInit];
    const headers = init.headers as Headers;
    expect(url.toString()).toBe("http://api:8080/api/feedback?source=manual");
    expect(headers.get("authorization")).toBe("Bearer trusted-session-token");
    expect(headers.get("cookie")).toBeNull();
    expect(headers.get("x-private-header")).toBeNull();
    expect(new TextDecoder().decode(init.body as ArrayBuffer)).toContain("Checkout issue");
  });

  it("rejects paths, origins, anonymous sessions, large bodies, and non-JSON mutations", async () => {
    const disallowed = await GET(new Request(
      "http://localhost:3000/api/backend/auth/login",
    ), context(["auth", "login"]));
    const untrusted = await POST(new Request(
      "http://localhost:3000/api/backend/feedback",
      {
        method: "POST",
        headers: { Origin: "https://attacker.example", "Content-Type": "application/json" },
        body: "{}",
      },
    ), context(["feedback"]));

    mocks.readSessionToken.mockResolvedValueOnce(undefined);
    const anonymous = await GET(new Request(
      "http://localhost:3000/api/backend/feedback",
    ), context(["feedback"]));
    const oversized = await POST(new Request(
      "http://localhost:3000/api/backend/feedback",
      {
        method: "POST",
        headers: {
          Origin: "http://localhost:3000",
          "Content-Type": "application/json",
          "Content-Length": "1048577",
        },
        body: "{}",
      },
    ), context(["feedback"]));
    const unsupported = await POST(new Request(
      "http://localhost:3000/api/backend/feedback",
      {
        method: "POST",
        headers: { Origin: "http://localhost:3000", "Content-Type": "text/plain" },
        body: "not-json",
      },
    ), context(["feedback"]));

    expect(disallowed.status).toBe(404);
    expect(untrusted.status).toBe(403);
    expect(anonymous.status).toBe(401);
    expect(oversized.status).toBe(413);
    expect(unsupported.status).toBe(415);
  });

  it("returns a safe unavailable problem when the backend cannot be reached", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("private network error")));

    const response = await GET(new Request(
      "http://localhost:3000/api/backend/dashboard/summary",
    ), context(["dashboard", "summary"]));
    const text = await response.text();

    expect(response.status).toBe(503);
    expect(text).not.toContain("private network error");
  });
});

function context(path: string[]): { params: Promise<{ path: string[] }> } {
  return { params: Promise.resolve({ path }) };
}
