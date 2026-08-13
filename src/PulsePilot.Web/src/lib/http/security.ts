const SEGMENT_PATTERN = /^[a-zA-Z0-9._~-]+$/;

const ALLOWED_METHODS: Record<string, ReadonlySet<string>> = {
  feedback: new Set(["GET", "POST", "PUT", "DELETE"]),
  clusters: new Set(["GET"]),
  actions: new Set(["GET", "POST"]),
  backlog: new Set(["GET"]),
  reports: new Set(["POST"]),
  copilot: new Set(["POST"]),
};

export function isAllowedBackendRequest(path: string[], method: string): boolean {
  if (path.length === 0 || path.length > 12) {
    return false;
  }

  if (
    path.some(
      (segment) =>
        !segment ||
        segment.length > 200 ||
        segment === "." ||
        segment === ".." ||
        !SEGMENT_PATTERN.test(segment),
    )
  ) {
    return false;
  }

  return ALLOWED_METHODS[path[0]]?.has(method.toUpperCase()) ?? false;
}

export function hasTrustedOrigin(request: Request): boolean {
  if (["GET", "HEAD", "OPTIONS"].includes(request.method.toUpperCase())) {
    return true;
  }

  const origin = request.headers.get("origin");
  if (!origin) {
    return false;
  }

  try {
    return new URL(origin).origin === new URL(request.url).origin;
  } catch {
    return false;
  }
}
