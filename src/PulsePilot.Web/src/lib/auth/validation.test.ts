import { describe, expect, it } from "vitest";

import { validateAuthPayload } from "./validation";

describe("validateAuthPayload", () => {
  it("normalizes a valid login without changing the password", () => {
    const result = validateAuthPayload("login", {
      email: "  Owner@PulsePilot.AI ",
      password: " keep-spaces ",
    });

    expect(result).toEqual({
      success: true,
      data: { email: "owner@pulsepilot.ai", password: " keep-spaces " },
    });
  });

  it("requires the backend password minimum for registration", () => {
    const result = validateAuthPayload("register", {
      email: "owner@pulsepilot.ai",
      displayName: "Owner",
      workspaceName: "PulsePilot",
      password: "too-short",
    });

    expect(result.success).toBe(false);
  });

  it("rejects malformed objects", () => {
    expect(validateAuthPayload("login", null).success).toBe(false);
    expect(validateAuthPayload("login", { email: "invalid", password: "secret" }).success).toBe(false);
  });
});
