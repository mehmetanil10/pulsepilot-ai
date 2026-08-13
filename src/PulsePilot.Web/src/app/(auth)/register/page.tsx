import type { Metadata } from "next";
import Link from "next/link";

import { AuthForm } from "@/components/auth/auth-form";

export const metadata: Metadata = { title: "Create workspace" };

export default function RegisterPage() {
  return (
    <div className="auth-card">
      <div className="auth-heading">
        <p className="eyebrow">Start listening</p>
        <h2>Create your workspace</h2>
        <p>Set up a shared home for product signals and engineering action.</p>
      </div>
      <AuthForm mode="register" />
      <p className="auth-switch">
        Already have a workspace? <Link href="/login">Sign in</Link>
      </p>
    </div>
  );
}
