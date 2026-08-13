import { describe, expect, it } from "vitest";

import { normalizeProblem } from "./problem";

describe("normalizeProblem", () => {
  it("keeps only bounded public problem fields", () => {
    expect(
      normalizeProblem(
        {
          title: "Validation failed",
          status: 400,
          traceId: "trace-1",
          internalException: "must not leak",
          errors: { Email: ["Email is invalid"] },
        },
        500,
      ),
    ).toEqual({
      title: "Validation failed",
      status: 400,
      traceId: "trace-1",
      detail: undefined,
      errors: { Email: ["Email is invalid"] },
    });
  });

  it("uses the transport status for malformed responses", () => {
    expect(normalizeProblem("not-json", 503)).toMatchObject({ status: 503 });
  });
});
