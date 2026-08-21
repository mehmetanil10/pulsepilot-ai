import { performance } from "node:perf_hooks";

const options = parseArguments(process.argv.slice(2));
const target = new URL(options.path, options.baseUrl);
const durations = [];
const statuses = new Map();
let nextRequest = 0;

const headers = { Accept: "application/json" };
if (process.env.PULSEPILOT_BEARER_TOKEN) {
  headers.Authorization = `Bearer ${process.env.PULSEPILOT_BEARER_TOKEN}`;
}

for (let index = 0; index < options.warmupRequests; index += 1) {
  await sendRequest(target, headers, options.timeoutMilliseconds);
}

const startedAt = performance.now();
await Promise.all(
  Array.from({ length: options.concurrency }, async () => {
    while (nextRequest < options.requests) {
      nextRequest += 1;
      const result = await sendRequest(
        target,
        headers,
        options.timeoutMilliseconds,
      );
      durations.push(result.durationMilliseconds);
      statuses.set(result.status, (statuses.get(result.status) ?? 0) + 1);
    }
  }),
);
const elapsedSeconds = (performance.now() - startedAt) / 1000;
const successfulRequests = [...statuses.entries()]
  .filter(([status]) => status >= 200 && status < 300)
  .reduce((total, [, count]) => total + count, 0);
const successRate = successfulRequests / options.requests;
const sortedDurations = durations.toSorted((left, right) => left - right);
const result = {
  target: target.toString(),
  requests: options.requests,
  concurrency: options.concurrency,
  successfulRequests,
  successRate: round(successRate, 4),
  requestsPerSecond: round(options.requests / elapsedSeconds, 2),
  latencyMilliseconds: {
    minimum: round(sortedDurations[0], 2),
    p50: round(percentile(sortedDurations, 0.5), 2),
    p95: round(percentile(sortedDurations, 0.95), 2),
    p99: round(percentile(sortedDurations, 0.99), 2),
    maximum: round(sortedDurations.at(-1), 2),
  },
  statuses: Object.fromEntries(
    [...statuses.entries()].sort(([left], [right]) => left - right),
  ),
  thresholds: {
    minimumSuccessRate: options.minimumSuccessRate,
    maximumP95Milliseconds: options.maximumP95Milliseconds,
  },
};

console.log(JSON.stringify(result, null, 2));

if (
  successRate < options.minimumSuccessRate ||
  result.latencyMilliseconds.p95 > options.maximumP95Milliseconds
) {
  process.exitCode = 1;
}

async function sendRequest(url, requestHeaders, timeoutMilliseconds) {
  const started = performance.now();

  try {
    const response = await fetch(url, {
      headers: requestHeaders,
      signal: AbortSignal.timeout(timeoutMilliseconds),
    });
    await response.arrayBuffer();

    return {
      status: response.status,
      durationMilliseconds: performance.now() - started,
    };
  } catch {
    return {
      status: 0,
      durationMilliseconds: performance.now() - started,
    };
  }
}

function percentile(values, ratio) {
  const index = Math.max(0, Math.ceil(values.length * ratio) - 1);
  return values[index];
}

function round(value, digits) {
  const factor = 10 ** digits;
  return Math.round(value * factor) / factor;
}

function parseArguments(args) {
  const values = new Map();

  for (let index = 0; index < args.length; index += 2) {
    const key = args[index];
    const value = args[index + 1];

    if (!key?.startsWith("--") || value === undefined) {
      throw new Error(`Invalid argument near '${key ?? "end of input"}'.`);
    }

    values.set(key.slice(2), value);
  }

  const baseUrl = new URL(values.get("base-url") ?? "http://localhost:8080");
  if (baseUrl.protocol !== "http:" && baseUrl.protocol !== "https:") {
    throw new Error("Base URL must use HTTP or HTTPS.");
  }

  const path = values.get("path") ?? "/health/live";
  if (!path.startsWith("/")) {
    throw new Error("Request path must start with '/'.");
  }

  return {
    baseUrl,
    path,
    requests: readInteger(values, "requests", 200, 1, 100_000),
    concurrency: readInteger(values, "concurrency", 20, 1, 1_000),
    warmupRequests: readInteger(values, "warmup", 10, 0, 1_000),
    timeoutMilliseconds: readInteger(values, "timeout-ms", 5_000, 100, 300_000),
    maximumP95Milliseconds: readNumber(values, "max-p95-ms", 250, 1, 300_000),
    minimumSuccessRate: readNumber(values, "min-success-rate", 1, 0, 1),
  };
}

function readInteger(values, key, fallback, minimum, maximum) {
  const value = Number(values.get(key) ?? fallback);
  if (!Number.isInteger(value) || value < minimum || value > maximum) {
    throw new Error(`--${key} must be an integer between ${minimum} and ${maximum}.`);
  }

  return value;
}

function readNumber(values, key, fallback, minimum, maximum) {
  const value = Number(values.get(key) ?? fallback);
  if (!Number.isFinite(value) || value < minimum || value > maximum) {
    throw new Error(`--${key} must be between ${minimum} and ${maximum}.`);
  }

  return value;
}
