import { NextResponse } from "next/server";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const GATEWAY = process.env.NEXT_PUBLIC_GATEWAY_URL || "http://localhost:5000";

async function proxy(path: string) {
  const url = `${GATEWAY}${path}`;
  console.log("[bibsy] proxying", url);
  const res = await fetch(url, {
    headers: { Accept: "application/json" },
    next: { revalidate: 60 },
  });
  console.log("[bibsy] status", res.status, "for", path);
  if (!res.ok) throw new Error(`Upstream ${path} failed: ${res.status}`);
  return res.json();
}

export async function POST(request: Request) {
  try {
    const { query } = await request.json();
    if (!query || typeof query !== "string") {
      return NextResponse.json({ answer: "Please ask me a question." }, { status: 400 });
    }

    const q = query.toLowerCase();

    if (q.includes("price") || q.includes("pricing") || q.includes("plan") || q.includes("package") || q.includes("cost") || q.includes("rate") || q.includes("subscription")) {
      try {
        const packages = await proxy("/gateway/system/lead-requests/pricing-packages");
        if (Array.isArray(packages) && packages.length) {
          const lines = packages.map((p: any) => `• ${p.name}: R${p.monthlyPrice}/month (R${p.annualPrice}/year) — ${p.description}`);
          return NextResponse.json({ answer: "Here are our current plans:\n" + lines.join("\n"), source: "api" });
        }
      } catch (e) {
        console.error("[bibsy] pricing error", e);
      }
    }

    return NextResponse.json({ answer: null, source: "knowledge" });
  } catch (e) {
    console.error("[bibsy] fatal", e);
    return NextResponse.json({ answer: "I'm having trouble reaching the server right now. Please try again in a moment.", source: "error" });
  }
}
