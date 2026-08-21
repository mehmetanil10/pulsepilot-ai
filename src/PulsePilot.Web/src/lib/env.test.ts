import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("server-only", () => ({}));

import { getApiBaseUrl } from "./env";

const originalApiUrl = process.env.PULSEPILOT_API_URL;

afterEach(() => {
  if (originalApiUrl === undefined) {
    delete process.env.PULSEPILOT_API_URL;
  } else {
    process.env.PULSEPILOT_API_URL = originalApiUrl;
  }
});

describe("getApiBaseUrl", () => {
  it("uses the local API origin by default", () => {
    delete process.env.PULSEPILOT_API_URL;

    expect(getApiBaseUrl().href).toBe("http://localhost:8080/");
  });

  it("accepts a private platform host and port", () => {
    process.env.PULSEPILOT_API_URL = "pulsepilot-api:8080";

    expect(getApiBaseUrl().href).toBe("http://pulsepilot-api:8080/");
  });

  it("preserves an explicit HTTPS origin", () => {
    process.env.PULSEPILOT_API_URL = "https://api.example.com";

    expect(getApiBaseUrl().href).toBe("https://api.example.com/");
  });

  it("rejects credentials and non-HTTP protocols", () => {
    process.env.PULSEPILOT_API_URL = "https://user:secret@api.example.com";
    expect(() => getApiBaseUrl()).toThrow(/HTTP\(S\)/);

    process.env.PULSEPILOT_API_URL = "file:///tmp/api";
    expect(() => getApiBaseUrl()).toThrow(/HTTP\(S\)/);
  });
});
