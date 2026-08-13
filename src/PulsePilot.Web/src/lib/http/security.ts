const SEGMENT_PATTERN = /^[a-zA-Z0-9._~-]+$/;

const ALLOWED_METHODS: Record<string, ReadonlySet<string>> = {
  feedback: new Set(["GET", "POST", "PUT", "DELETE"]),
  clusters: new Set(["GET"]),
  actions: new Set(["GET", "POST"]),
  backlog: new Set(["GET"]),
  reports: new Set(["POST"]),
  copilot: new Set(["POST"]),
  dashboard: new Set(["GET"]),
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
    const requestUrl = new URL(request.url);
    const originUrl = new URL(origin);

    if (originUrl.origin === requestUrl.origin) {
      return true;
    }

    // Next's standalone server can expose its internal listen port in
    // request.url while the public port remains available in the Host header.
    // Comparing against that public origin keeps same-origin mutations working
    // behind Docker port mappings and ordinary reverse proxies.
    const host = request.headers.get("host")?.trim();
    if (!host || host.includes(",")) {
      return false;
    }

    const forwardedProtocol = request.headers
      .get("x-forwarded-proto")
      ?.split(",", 1)[0]
      .trim()
      .toLowerCase();
    const protocol = forwardedProtocol ?? requestUrl.protocol.slice(0, -1);

    if (protocol !== "http" && protocol !== "https") {
      return false;
    }

    return originUrl.origin === new URL(`${protocol}://${host}`).origin;
  } catch {
    return false;
  }
}
