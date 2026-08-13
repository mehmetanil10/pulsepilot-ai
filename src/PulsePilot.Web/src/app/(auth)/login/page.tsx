import type { Metadata } from "next";
import Link from "next/link";

import { AuthForm } from "@/components/auth/auth-form";

export const metadata: Metadata = { title: "Sign in" };

export default function LoginPage() {
  return (
    <div className="auth-card">
      <div className="auth-heading">
        <p className="eyebrow">Welcome back</p>
        <h2>Sign in to your workspace</h2>
        <p>Pick up where your product signals left off.</p>
      </div>
      <AuthForm mode="login" />
      <p className="auth-switch">
        New to PulsePilot? <Link href="/register">Create a workspace</Link>
      </p>
    </div>
  );
}
