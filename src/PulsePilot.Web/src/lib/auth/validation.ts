export type AuthMode = "login" | "register";

export type LoginPayload = {
  email: string;
  password: string;
};

export type RegisterPayload = LoginPayload & {
  displayName: string;
  workspaceName: string;
};

export type AuthPayload = LoginPayload | RegisterPayload;

export type ValidationResult =
  | { success: true; data: AuthPayload }
  | { success: false; detail: string };

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function validateAuthPayload(mode: AuthMode, value: unknown): ValidationResult {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    return invalid("Form verileri geçersiz.");
  }

  const input = value as Record<string, unknown>;
  const email = text(input.email).toLowerCase();
  const password = typeof input.password === "string" ? input.password : "";

  if (!email || email.length > 320 || !EMAIL_PATTERN.test(email)) {
    return invalid("Geçerli bir e-posta adresi girin.");
  }

  if (!password || password.length > 128) {
    return invalid("Şifre zorunludur ve en fazla 128 karakter olabilir.");
  }

  if (mode === "login") {
    return { success: true, data: { email, password } };
  }

  const displayName = text(input.displayName);
  const workspaceName = text(input.workspaceName);

  if (!displayName || displayName.length > 120) {
    return invalid("Ad soyad zorunludur ve en fazla 120 karakter olabilir.");
  }

  if (!workspaceName || workspaceName.length > 150) {
    return invalid("Workspace adı zorunludur ve en fazla 150 karakter olabilir.");
  }

  if (password.length < 12) {
    return invalid("Şifre en az 12 karakter olmalıdır.");
  }

  return {
    success: true,
    data: { email, password, displayName, workspaceName },
  };
}

function text(value: unknown): string {
  return typeof value === "string" ? value.trim() : "";
}

function invalid(detail: string): ValidationResult {
  return { success: false, detail };
}
