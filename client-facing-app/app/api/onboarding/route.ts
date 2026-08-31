import { NextResponse } from "next/server";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const IDENTITY_API = process.env.LEAD_API_URL
  ? process.env.LEAD_API_URL.replace("/api/system/lead-requests", "/api/auth")
  : "http://localhost:5001/api/auth";

export async function POST(request: Request) {
  try {
    const body = await request.json();

    const res = await fetch(`${IDENTITY_API}/trial/onboarding`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });

    const data = await res.json();

    if (!res.ok) {
      return NextResponse.json(
        { message: data.message || "Onboarding failed." },
        { status: res.status }
      );
    }

    return NextResponse.json(data, { status: 200 });
  } catch {
    return NextResponse.json(
      { message: "Unable to reach the server. Please try again later." },
      { status: 502 }
    );
  }
}
