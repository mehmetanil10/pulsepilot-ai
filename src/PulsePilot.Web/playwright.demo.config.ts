import { defineConfig, devices } from "@playwright/test";

const baseURL = process.env.PLAYWRIGHT_BASE_URL ?? "http://127.0.0.1:3000";

export default defineConfig({
  testDir: "./demo",
  fullyParallel: false,
  forbidOnly: true,
  retries: 0,
  workers: 1,
  reporter: [["list"]],
  outputDir: "../../artifacts/demo/test-results",
  use: {
    ...devices["Desktop Chrome"],
    baseURL,
    colorScheme: "light",
    locale: "en-US",
    timezoneId: "UTC",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    video: { mode: "on", size: { width: 1440, height: 900 } },
    viewport: { width: 1440, height: 900 },
  },
  projects: [{ name: "demo-chromium" }],
});
