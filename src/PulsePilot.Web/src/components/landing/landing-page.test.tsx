// @vitest-environment jsdom

import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { LandingPage } from "./landing-page";

describe("LandingPage", () => {
  it("introduces the public product honestly and links anonymous visitors to the demo sign-in", () => {
    render(<LandingPage authenticated={false} />);

    expect(screen.getByRole("heading", {
      name: /From customer signals to accountable engineering action/i,
    })).toBeInTheDocument();
    expect(screen.getAllByRole("link", { name: "Explore the demo" })[0]).toHaveAttribute("href", "/login");
    expect(screen.getByText(/Native Zendesk, Intercom, app-store, and survey connectors are roadmap work/i)).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "AI proposes. Your team decides." })).toBeInTheDocument();
  });

  it("offers an authenticated visitor a direct path back to the workspace", () => {
    render(<LandingPage authenticated />);

    expect(screen.getAllByRole("link", { name: "Open dashboard" })[0]).toHaveAttribute("href", "/dashboard");
    expect(screen.queryByRole("link", { name: "Sign in" })).not.toBeInTheDocument();
  });
});
