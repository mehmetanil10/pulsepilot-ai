import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    environment: "node",
    include: ["src/**/*.test.ts"],
    coverage: {
      provider: "v8",
      all: true,
      include: ["src/**/*.{ts,tsx}"],
      exclude: ["src/**/*.test.ts", "src/types/**"],
      reporter: ["text", "json-summary", "lcov"],
      reportsDirectory: "../../artifacts/quality/web-coverage",
    },
  },
});
