import { readSessionToken } from "@/lib/auth/session";
import { getApiBaseUrl } from "@/lib/env";
import { problemResponse } from "@/lib/http/problem";
import { hasTrustedOrigin, isAllowedBackendRequest } from "@/lib/http/security";

const MAX_PROXY_BODY_BYTES = 1_048_576;

async function proxyRequest(
  request: Request,
  context: { params: Promise<{ path: string[] }> },
): Promise<Response> {
  const method = request.method.toUpperCase();
  const { path } = await context.params;

  if (!isAllowedBackendRequest(path, method)) {
    return problemResponse(404);
  }

  if (!hasTrustedOrigin(request)) {
    return problemResponse(403, undefined, "İstek kaynağı doğrulanamadı.");
  }

  const token = await readSessionToken();
  if (!token) {
    return problemResponse(401);
  }

  const contentLength = Number(request.headers.get("content-length") ?? 0);
  if (contentLength > MAX_PROXY_BODY_BYTES) {
    return problemResponse(413);
  }

  const requestUrl = new URL(request.url);
  const backendUrl = new URL(`api/${path.map(encodeURIComponent).join("/")}`, getApiBaseUrl());
  backendUrl.search = requestUrl.search;

  const headers = new Headers({
    Accept: request.headers.get("accept") ?? "application/json",
    Authorization: `Bearer ${token}`,
  });
  const contentType = request.headers.get("content-type");
  if (contentType) {
    headers.set("Content-Type", contentType);
  }

  let body: ArrayBuffer | undefined;
  if (!['GET', 'HEAD'].includes(method)) {
    body = await request.arrayBuffer();
    if (body.byteLength > MAX_PROXY_BODY_BYTES) {
      return problemResponse(413);
    }
    if (body.byteLength > 0 && contentType?.split(";", 1)[0].trim() !== "application/json") {
      return problemResponse(415);
    }
  }

  try {
    const backendResponse = await fetch(backendUrl, {
      method,
      headers,
      body,
      cache: "no-store",
      redirect: "manual",
      signal: AbortSignal.timeout(60_000),
    });

    const responseHeaders = new Headers({ "Cache-Control": "no-store" });
    const backendContentType = backendResponse.headers.get("content-type");
    if (backendContentType) {
      responseHeaders.set("Content-Type", backendContentType);
    }
    const requestId = backendResponse.headers.get("x-request-id");
    if (requestId) {
      responseHeaders.set("X-Request-Id", requestId);
    }

    return new Response(backendResponse.body, {
      status: backendResponse.status,
      headers: responseHeaders,
    });
  } catch {
    return problemResponse(503);
  }
}

export const GET = proxyRequest;
export const POST = proxyRequest;
export const PUT = proxyRequest;
export const DELETE = proxyRequest;
