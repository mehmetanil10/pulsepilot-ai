import { expect, test, type Page } from "@playwright/test";
import path from "node:path";

const demoEmail = process.env.PULSEPILOT_DEMO_EMAIL ?? "demo@pulsepilot.ai";
const demoPassword = process.env.PULSEPILOT_DEMO_PASSWORD;
const screenshotRoot = path.resolve(__dirname, "../../../docs/assets/screenshots");

test("capture the seeded PulsePilot product tour", async ({ page }) => {
  test.skip(!demoPassword, "PULSEPILOT_DEMO_PASSWORD is required.");

  await page.goto("/login");
  await page.getByLabel("Work email").fill(demoEmail);
  await page.getByLabel("Password").fill(demoPassword!);
  await page.getByRole("button", { name: "Sign in" }).click();

  await expect(page).toHaveURL(/\/dashboard$/);
  await expect(page.getByRole("heading", { name: /Good to see you,/ })).toBeVisible();
  await page.getByRole("link", { name: "30d", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\?periodDays=30$/);
  await capture(page, "dashboard.png");

  await page.getByRole("link", { name: "Feedback", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Feedback intelligence" })).toBeVisible();
  await capture(page, "feedback-library.png");

  const firstFeedback = page.locator(".feedback-row").first();
  await expect(firstFeedback).toBeVisible();
  await firstFeedback.click();
  await expect(page.getByRole("region", { name: "Analysis" })).toBeVisible();
  await capture(page, "feedback-analysis.png");

  await page.getByRole("link", { name: "Actions", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Review AI actions" })).toBeVisible();
  await capture(page, "human-review.png");

  await page.getByRole("link", { name: "Backlog", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Engineering backlog", exact: true })).toBeVisible();
  await capture(page, "engineering-backlog.png");

  await page.getByRole("link", { name: "Copilot", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Workspace Copilot" })).toBeVisible();
  await capture(page, "workspace-copilot.png");
});

async function capture(page: Page, fileName: string) {
  await page.evaluate(async () => { await document.fonts.ready; });
  await page.waitForTimeout(700);
  await page.screenshot({
    path: path.join(screenshotRoot, fileName),
    fullPage: false,
    animations: "disabled",
  });
}
