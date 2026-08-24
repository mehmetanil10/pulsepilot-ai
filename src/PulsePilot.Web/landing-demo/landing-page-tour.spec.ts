import { expect, test, type Locator, type Page } from "@playwright/test";

test("record the public PulsePilot landing page story", async ({ page }) => {
  test.setTimeout(60_000);

  await page.goto("/");
  await page.evaluate(async () => { await document.fonts.ready; });

  const hero = page.getByRole("heading", {
    name: /From customer signals to accountable engineering action/i,
  });
  await expect(hero).toBeVisible();
  await expect(page.getByRole("link", { name: "Explore the demo" }).first()).toBeVisible();
  await page.waitForTimeout(3_000);

  await reveal(page, page.getByRole("heading", {
    name: "Your customers are already telling you what to build next.",
  }));
  await reveal(page, page.getByRole("heading", {
    name: "See the signal before it becomes noise.",
  }), 3_000);
  await reveal(page, page.getByRole("heading", {
    name: /Go from “customers are unhappy” to a decision-ready brief/i,
  }));
  await reveal(page, page.getByRole("heading", {
    name: "AI proposes. Your team decides.",
  }), 3_000);
  await reveal(page, page.getByRole("heading", {
    name: "Ask your product what customers need next.",
  }));
  await reveal(page, page.getByRole("heading", {
    name: "A portfolio project with production-shaped foundations.",
  }), 3_000);
  await reveal(page, page.getByRole("heading", {
    name: "Demo-ready today. Integration-ready by design.",
  }));
  await reveal(page, page.getByRole("heading", {
    name: "Give every customer signal a path to action.",
  }), 3_500);

  await expect(page.getByRole("link", { name: "View source on GitHub" })).toBeVisible();
});

async function reveal(page: Page, locator: Locator, dwell = 2_500) {
  await locator.waitFor({ state: "attached" });
  await locator.evaluate((element) => {
    element.scrollIntoView({ behavior: "smooth", block: "center" });
  });
  await page.waitForTimeout(1_100);
  await expect(locator).toBeVisible();
  await page.waitForTimeout(dwell);
}
