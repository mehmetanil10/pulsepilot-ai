import "server-only";

const DEFAULT_API_URL = "http://localhost:8080";

export function getApiBaseUrl(): URL {
  const configured = process.env.PULSEPILOT_API_URL?.trim() || DEFAULT_API_URL;
  const url = new URL(configured);

  if (
    !["http:", "https:"].includes(url.protocol) ||
    url.username ||
    url.password ||
    url.search ||
    url.hash
  ) {
    throw new Error("PULSEPILOT_API_URL must be an HTTP(S) origin.");
  }

  url.pathname = `${url.pathname.replace(/\/$/, "")}/`;
  return url;
}
