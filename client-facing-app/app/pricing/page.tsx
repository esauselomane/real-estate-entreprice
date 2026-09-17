import { Check, Star, ServerOff } from "lucide-react";
import Link from "next/link";
export const dynamic = "force-dynamic";

type PricingPlan = {
  code: string; name: string; description: string; monthlyPrice: number | null;
  annualPrice: number | null; features: string[]; highlighted?: boolean;
};

async function loadPricing(): Promise<PricingPlan[]> {
  const gateway = process.env.NEXT_PUBLIC_GATEWAY_URL || "http://localhost:5000";
  try {
    const response = await fetch(`${gateway}/gateway/system/lead-requests/pricing-packages`, { cache: "no-store" });
    if (!response.ok) return [];
    return await response.json();
  } catch { return []; }
}

export const metadata = {
  title: "Pricing",
  description:
    "Simple, transparent pricing for agencies of every size. Start with SME Agency and scale to Enterprise.",
};

export default async function PricingPage() {
  const pricing = await loadPricing();
  const application = process.env.NEXT_PUBLIC_APP_URL || "http://localhost:5173";
  return (
    <section className="py-10 sm:py-12">
      <div className="container">
        <div className="mx-auto max-w-3xl text-center">
          <h1 className="text-4xl font-display font-extrabold tracking-tight text-white sm:text-5xl">
            Pricing
          </h1>
          <p className="mt-4 text-lg text-slate-300">
            Hosted access to inventory, pipeline, CRM, brochures, and administration. Choose a plan that matches your agency size.
          </p>
        </div>

        <div className="mt-6">
          <div className="mx-auto max-w-2xl rounded-2xl border border-emerald-700 bg-emerald-900/30 p-4 text-center">
            <div className="flex items-center justify-center gap-2 text-emerald-300">
              <ServerOff className="h-5 w-5" />
              <p className="text-sm font-semibold">
                Fully hosted SaaS — no servers, no hosting fees, no IT overhead.
                Save thousands in infrastructure and maintenance costs.
              </p>
            </div>
          </div>
        </div>

        {pricing.length === 0 && <p className="mt-10 text-center">Pricing is currently unavailable. <Link href="/contact">Contact sales for a quote.</Link></p>}
        <div className="mt-10 grid gap-6 lg:grid-cols-3">
          {pricing.map((plan) => (
            <div
              key={plan.code}
              className={`relative flex flex-col rounded-3xl border p-8 shadow-lg shadow-black/20 ${
                plan.highlighted
                  ? "border-blue-500 bg-slate-800/60 ring-1 ring-blue-500"
                  : "border-slate-700 bg-slate-800/60"
              }`}
            >
              {plan.highlighted && (
                <div className="absolute -top-3 left-1/2 -translate-x-1/2">
                  <span className="inline-flex items-center gap-1 rounded-full bg-blue-600 px-3 py-1 text-xs font-extrabold text-white">
                    <Star className="h-3 w-3" />
                    Most popular
                  </span>
                </div>
              )}

              <div>
                <h2 className="text-xl font-display font-bold text-white">
                  {plan.name}
                </h2>
                <p className="mt-2 text-sm text-slate-300">
                  {plan.description}
                </p>
              </div>

              <div className="mt-6">
                <span className="text-4xl font-extrabold text-white">
                  {plan.code === "ENTERPRISE" ? "Contact sales" : `R${plan.monthlyPrice}`}
                </span>
                {plan.code !== "ENTERPRISE" && <span className="text-sm text-slate-400">/month</span>}
                {plan.code !== "ENTERPRISE" && plan.annualPrice != null && <p className="mt-1 text-xs text-slate-400">or R{plan.annualPrice}/year</p>}
              </div>

              <ul className="mt-8 space-y-3 text-sm text-slate-300">
                {plan.features.map((feature) => (
                  <li key={feature} className="flex gap-3">
                    <Check className="mt-0.5 h-4 w-4 shrink-0 text-emerald-400" />
                    <span>{feature}</span>
                  </li>
                ))}
              </ul>

              <div className="mt-8">
                <Link
                  href={plan.code === "ENTERPRISE" ? "/contact?package=ENTERPRISE" : `${application}/?billing=1&package=${encodeURIComponent(plan.code)}`}
                  className={`block w-full rounded-xl py-3 text-center text-sm font-semibold transition-all duration-200 ease-in-out hover:scale-105 hover:shadow-lg active:scale-95 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 ${
                    plan.highlighted
                      ? "bg-blue-600 text-white hover:bg-blue-500 focus-visible:outline-blue-500"
                      : "bg-slate-800 text-white hover:bg-slate-700 focus-visible:outline-slate-700"
                  }`}
                >
                  Get started with {plan.name}
                </Link>
              </div>
            </div>
          ))}
        </div>

        <div className="mt-10 text-center">
          <p className="text-sm text-slate-400">
            All prices in ZAR. VAT excluded. Need a custom plan?{" "}
            <Link href="/contact" className="text-blue-400 hover:text-blue-400">
              Talk to us
            </Link>
            .
          </p>
        </div>
      </div>
    </section>
  );
}
