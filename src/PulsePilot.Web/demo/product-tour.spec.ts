import { expect, test, type Page } from "@playwright/test";
import path from "node:path";

const demoEmail = process.env.PULSEPILOT_DEMO_EMAIL ?? "demo@pulsepilot.ai";
const demoPassword = process.env.PULSEPILOT_DEMO_PASSWORD;
const screenshotRoot = path.resolve(__dirname, "../../../docs/assets/screenshots");

test("capture the seeded PulsePilot product tour", async ({ page }) => {
  test.skip(!demoPassword, "PULSEPILOT_DEMO_PASSWORD is required.");

  await page.goto("/");
  await expect(page.getByRole("heading", {
    name: /From customer signals to accountable engineering action/i,
  })).toBeVisible();
  await capture(page, "landing-page.png");

  await page.goto("/login");
  await page.getByLabel("Work email").fill(demoEmail);
  await page.getByLabel("Password").fill(demoPassword!);
  await page.getByRole("button", { name: "Sign in" }).click();

  await expect(page).toHaveURL(/\/dashboard$/);
  await expect(page.getByRole("heading", { name: /Good to see you,/ })).toBeVisible();
  await page.getByRole("link", { name: "30d", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\?periodDays=30$/);
  await expect(page.locator(".live-kpi-grid")).toContainText("76");
  await expect(page.locator(".trending-list")).toContainText("SSO and login reliability");
  await expect(page.locator(".actions-panel")).toContainText("2 total");
  await capture(page, "dashboard.png");

  await page.getByRole("link", { name: "Feedback", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Feedback intelligence" })).toBeVisible();
  await expect(page.locator(".feedback-count")).toContainText("100");
  await expect(page.locator(".feedback-row")).toHaveCount(12);
  await capture(page, "feedback-library.png");

  const firstFeedback = page.locator(".feedback-row").first();
  await expect(firstFeedback).toBeVisible();
  await firstFeedback.click();
  await expect(page.getByRole("region", { name: "Analysis" })).toBeVisible();
  await expect(page.getByRole("region", { name: "Analysis" })).toContainText("94% confidence");
  await expect(page.getByText("Associated cluster", { exact: true })).toBeVisible();
  await capture(page, "feedback-analysis.png");

  await page.getByRole("link", { name: "Actions", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Review AI actions" })).toBeVisible();
  await expect(page.locator(".pending-action-card")).toHaveCount(2);
  await expect(page.locator(".pending-action-list")).toContainText("Dashboard data freshness");
  await expect(page.locator(".pending-action-list")).toContainText("Checkout payment failures");
  await capture(page, "human-review.png");

  await page.getByRole("link", { name: "Backlog", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Engineering backlog", exact: true })).toBeVisible();
  await expect(page.locator(".backlog-count")).toContainText("1");
  await expect(page.locator(".backlog-card")).toContainText("SSO and login reliability");
  await capture(page, "engineering-backlog.png");

  await page.getByRole("link", { name: "Copilot", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Workspace Copilot" })).toBeVisible();
  await expect(page.locator(".copilot-safety-note")).toContainText("Human control stays on");
  await expect(page.locator(".copilot-capabilities")).toContainText("Read-only and analytical");
  await capture(page, "workspace-copilot.png");

  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page).toHaveURL(/\/login$/);
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
