export const dynamic = "force-dynamic";

export function GET(): Response {
  return Response.json(
    { status: "healthy", service: "pulsepilot-web" },
    { headers: { "Cache-Control": "no-store" } },
  );
}
