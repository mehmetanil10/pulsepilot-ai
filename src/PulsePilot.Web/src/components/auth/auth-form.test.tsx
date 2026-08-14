// @vitest-environment jsdom

import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

const router = vi.hoisted(() => ({ replace: vi.fn(), refresh: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => router }));

import { AuthForm } from "./auth-form";

describe("AuthForm", () => {
  beforeEach(() => {
    router.replace.mockReset();
    router.refresh.mockReset();
  });

  it("submits registration data and enters the workspace", async () => {
    const fetchMock = vi.fn().mockResolvedValue(Response.json({ user: {} }, { status: 201 }));
    vi.stubGlobal("fetch", fetchMock);
    render(<AuthForm mode="register" />);

    fireEvent.change(screen.getByLabelText("Your name"), { target: { value: "Ada Lovelace" } });
    fireEvent.change(screen.getByLabelText("Workspace"), { target: { value: "Analytical Engine" } });
    fireEvent.change(screen.getByLabelText("Work email"), { target: { value: "ada@example.com" } });
    fireEvent.change(screen.getByLabelText("Password"), { target: { value: "correct-horse-battery" } });
    fireEvent.click(screen.getByRole("button", { name: /Create workspace/ }));

    await waitFor(() => expect(router.replace).toHaveBeenCalledWith("/dashboard"));
    expect(router.refresh).toHaveBeenCalledOnce();
    expect(fetchMock).toHaveBeenCalledWith("/api/auth/register", expect.objectContaining({
      method: "POST",
      body: JSON.stringify({
        displayName: "Ada Lovelace",
        workspaceName: "Analytical Engine",
        email: "ada@example.com",
        password: "correct-horse-battery",
      }),
    }));
  });

  it("shows bounded backend validation feedback", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(Response.json({
      title: "Validation failed",
      status: 400,
      errors: { Email: ["Enter a valid work email."] },
    }, { status: 400 })));
    render(<AuthForm mode="login" />);

    fireEvent.change(screen.getByLabelText("Work email"), { target: { value: "invalid" } });
    fireEvent.change(screen.getByLabelText("Password"), { target: { value: "password" } });
    fireEvent.click(screen.getByRole("button", { name: /Sign in/ }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Enter a valid work email.");
    expect(router.replace).not.toHaveBeenCalled();
  });

  it("reports transport failures without leaking the exception", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("private network detail")));
    render(<AuthForm mode="login" />);

    fireEvent.change(screen.getByLabelText("Work email"), { target: { value: "owner@example.com" } });
    fireEvent.change(screen.getByLabelText("Password"), { target: { value: "password" } });
    fireEvent.click(screen.getByRole("button", { name: /Sign in/ }));

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("PulsePilot could not be reached");
    expect(alert).not.toHaveTextContent("private network detail");
  });
});
