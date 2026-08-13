export type PublicProblem = {
  title: string;
  detail?: string;
  status: number;
  traceId?: string;
  errors?: Record<string, string[]>;
};

const MAX_MESSAGE_LENGTH = 500;

function safeText(value: unknown): string | undefined {
  if (typeof value !== "string") {
    return undefined;
  }

  const trimmed = value.trim();
  return trimmed ? trimmed.slice(0, MAX_MESSAGE_LENGTH) : undefined;
}

function safeErrors(value: unknown): Record<string, string[]> | undefined {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    return undefined;
  }

  const result: Record<string, string[]> = {};
  for (const [key, messages] of Object.entries(value).slice(0, 20)) {
    if (!Array.isArray(messages)) {
      continue;
    }

    const safeMessages = messages
      .map(safeText)
      .filter((message): message is string => Boolean(message))
      .slice(0, 10);
    if (safeMessages.length > 0) {
      result[key.slice(0, 100)] = safeMessages;
    }
  }

  return Object.keys(result).length > 0 ? result : undefined;
}

export function normalizeProblem(value: unknown, fallbackStatus: number): PublicProblem {
  const input = value && typeof value === "object" ? (value as Record<string, unknown>) : {};
  const status =
    typeof input.status === "number" && input.status >= 400 && input.status <= 599
      ? input.status
      : fallbackStatus;

  return {
    title: safeText(input.title) ?? defaultTitle(status),
    detail: safeText(input.detail),
    status,
    traceId: safeText(input.traceId),
    errors: safeErrors(input.errors),
  };
}

export async function readProblem(response: Response): Promise<PublicProblem> {
  try {
    return normalizeProblem(await response.json(), response.status);
  } catch {
    return normalizeProblem(undefined, response.status);
  }
}

export function problemResponse(
  status: number,
  title = defaultTitle(status),
  detail?: string,
): Response {
  return Response.json(normalizeProblem({ title, detail, status }, status), {
    status,
    headers: { "Cache-Control": "no-store" },
  });
}

function defaultTitle(status: number): string {
  if (status === 400) return "Geçersiz istek";
  if (status === 401) return "Oturum gerekli";
  if (status === 403) return "Bu işlem için yetkiniz yok";
  if (status === 404) return "Kaynak bulunamadı";
  if (status === 409) return "İstek mevcut durumla çakışıyor";
  if (status === 413) return "İstek gövdesi çok büyük";
  if (status === 415) return "Desteklenmeyen içerik türü";
  if (status === 429) return "Çok fazla istek";
  if (status === 502) return "API geçersiz bir yanıt verdi";
  if (status === 503) return "PulsePilot API şu anda kullanılamıyor";
  return "İstek tamamlanamadı";
}
