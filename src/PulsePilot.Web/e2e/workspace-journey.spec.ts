import { expect, test } from "@playwright/test";

test("anonymous visitors see the public product story while workspace routes remain protected", async ({ page }) => {
  await page.goto("/");

  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByRole("heading", {
    name: /From customer signals to accountable engineering action/i,
  })).toBeVisible();
  await expect(page.getByRole("link", { name: "Explore the demo" }).first()).toHaveAttribute("href", "/login");

  await page.goto("/dashboard");

  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByRole("heading", { name: "Sign in to your workspace" })).toBeVisible();
});

test("owner can register, ingest feedback, inspect workspace views, and sign out", async ({ page }) => {
  const unique = `${Date.now()}-${test.info().workerIndex}`;
  const feedbackTitle = `E2E checkout regression ${unique}`;

  await page.goto("/register");
  await page.getByLabel("Your name").fill("E2E Owner");
  await page.getByLabel("Workspace").fill(`E2E Workspace ${unique}`);
  await page.getByLabel("Work email").fill(`e2e-${unique}@example.com`);
  await page.getByLabel("Password").fill("correct-horse-battery-staple");
  await page.getByRole("button", { name: "Create workspace" }).click();

  await expect(page).toHaveURL(/\/dashboard$/);
  await expect(page.getByRole("heading", { name: "Good to see you, E2E." })).toBeVisible();

  await page.goto("/");
  await expect(page.getByRole("link", { name: "Open dashboard" }).first()).toBeVisible();
  await page.getByRole("link", { name: "Open dashboard" }).first().click();
  await expect(page).toHaveURL(/\/dashboard$/);

  const created = await page.evaluate(async (title) => {
    const response = await fetch("/api/backend/feedback", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        title,
        content: "Checkout freezes after a valid payment card is submitted.",
        source: "manual",
        customerName: "Acceptance Customer",
        customerEmail: "acceptance.customer@example.com",
      }),
    });
    return { status: response.status, body: await response.json() };
  }, feedbackTitle);

  expect(created.status).toBe(201);
  expect(created.body).toMatchObject({ title: feedbackTitle, processingStatus: "pending" });

  await page.getByRole("link", { name: "Feedback", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Feedback intelligence" })).toBeVisible();
  await page.getByRole("link", { name: new RegExp(feedbackTitle) }).click();
  await expect(page.getByRole("heading", { name: feedbackTitle })).toBeVisible();
  await expect(page.getByRole("region", { name: "Analysis" }))
    .toContainText("Analysis is being prepared.");

  await page.getByRole("link", { name: "Actions", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Review AI actions" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "The review queue is clear." })).toBeVisible();

  await page.getByRole("link", { name: "Backlog", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Engineering backlog", exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: "The engineering backlog is clear." })).toBeVisible();

  await page.getByRole("link", { name: "Copilot", exact: true }).click();
  await page.getByLabel(/Ask about feedback/).fill("What changed this week?");
  await page.getByRole("button", { name: "Ask Copilot" }).click();
  await expect(page.getByRole("alert").filter({
    hasText: "Copilot could not complete this answer.",
  })).toContainText("Copilot is temporarily unavailable");

  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page).toHaveURL(/\/login$/);
  await page.goto("/dashboard");
  await expect(page).toHaveURL(/\/login$/);
});
