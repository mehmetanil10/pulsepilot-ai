"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

import { Icon } from "@/components/icons";
import { readProblem } from "@/lib/http/problem";

type AuthFormProps = {
  mode: "login" | "register";
};

export function AuthForm({ mode }: AuthFormProps) {
  const router = useRouter();
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const isRegister = mode === "register";

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPending(true);
    setError(null);

    const formData = new FormData(event.currentTarget);
    const payload = Object.fromEntries(formData.entries());

    try {
      const response = await fetch(`/api/auth/${mode}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });

      if (!response.ok) {
        const problem = await readProblem(response);
        const validationMessage = problem.errors
          ? Object.values(problem.errors).flat()[0]
          : undefined;
        setError(validationMessage ?? problem.detail ?? problem.title);
        return;
      }

      router.replace("/dashboard");
      router.refresh();
    } catch {
      setError("PulsePilot could not be reached. Check your connection and try again.");
    } finally {
      setPending(false);
    }
  }

  return (
    <form className="auth-form" onSubmit={submit} noValidate>
      {isRegister && (
        <div className="field-grid">
          <Field
            id="displayName"
            name="displayName"
            label="Your name"
            placeholder="Ada Lovelace"
            autoComplete="name"
            maxLength={120}
          />
          <Field
            id="workspaceName"
            name="workspaceName"
            label="Workspace"
            placeholder="Acme Product"
            autoComplete="organization"
            maxLength={150}
          />
        </div>
      )}
      <Field
        id={`${mode}-email`}
        name="email"
        label="Work email"
        placeholder="you@company.com"
        type="email"
        autoComplete="email"
        maxLength={320}
      />
      <Field
        id={`${mode}-password`}
        name="password"
        label="Password"
        placeholder={isRegister ? "At least 12 characters" : "Your password"}
        type="password"
        autoComplete={isRegister ? "new-password" : "current-password"}
        minLength={isRegister ? 12 : undefined}
        maxLength={128}
      />
      {error && (
        <div className="form-error" role="alert">
          <span aria-hidden="true">!</span>
          <p>{error}</p>
        </div>
      )}
      <button className="primary-button" type="submit" disabled={pending}>
        <span>{pending ? "Working…" : isRegister ? "Create workspace" : "Sign in"}</span>
        {!pending && <Icon name="arrow" />}
      </button>
      {isRegister && (
        <p className="form-note">
          By continuing, you agree to keep customer data protected within your workspace.
        </p>
      )}
    </form>
  );
}

type FieldProps = React.InputHTMLAttributes<HTMLInputElement> & {
  id: string;
  label: string;
};

function Field({ id, label, ...props }: FieldProps) {
  return (
    <label className="form-field" htmlFor={id}>
      <span>{label}</span>
      <input id={id} required {...props} />
    </label>
  );
}
