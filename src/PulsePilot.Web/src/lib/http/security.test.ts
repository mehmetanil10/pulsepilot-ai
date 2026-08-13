import { describe, expect, it } from "vitest";

import { hasTrustedOrigin, isAllowedBackendRequest } from "./security";

describe("backend gateway allowlist", () => {
  it("allows only supported methods on known API roots", () => {
    expect(isAllowedBackendRequest(["feedback"], "GET")).toBe(true);
    expect(isAllowedBackendRequest(["actions", "action-id", "approve"], "POST")).toBe(true);
    expect(isAllowedBackendRequest(["backlog"], "POST")).toBe(false);
    expect(isAllowedBackendRequest(["auth", "login"], "POST")).toBe(false);
    expect(isAllowedBackendRequest(["copilot", "chat"], "DELETE")).toBe(false);
  });

  it("rejects traversal and encoded path material", () => {
    expect(isAllowedBackendRequest(["feedback", ".."], "GET")).toBe(false);
    expect(isAllowedBackendRequest(["feedback", "item/analysis"], "GET")).toBe(false);
    expect(isAllowedBackendRequest([], "GET")).toBe(false);
  });
});

describe("same-origin mutation check", () => {
  it("accepts matching origins and read-only requests", () => {
    expect(
      hasTrustedOrigin(
        new Request("https://app.pulsepilot.ai/api/auth/login", {
          method: "POST",
          headers: { Origin: "https://app.pulsepilot.ai" },
        }),
      ),
    ).toBe(true);
    expect(hasTrustedOrigin(new Request("https://app.pulsepilot.ai/api/health"))).toBe(true);
  });

  it("rejects missing or cross-origin mutation origins", () => {
    expect(
      hasTrustedOrigin(
        new Request("https://app.pulsepilot.ai/api/auth/login", { method: "POST" }),
      ),
    ).toBe(false);
    expect(
      hasTrustedOrigin(
        new Request("https://app.pulsepilot.ai/api/auth/login", {
          method: "POST",
          headers: { Origin: "https://attacker.example" },
        }),
      ),
    ).toBe(false);
  });
});
