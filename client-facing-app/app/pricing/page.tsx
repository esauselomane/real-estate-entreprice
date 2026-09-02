import { Check, Star, ServerOff } from "lucide-react";
import Link from "next/link";
import { pricing } from "@/lib/content";

export const metadata = {
  title: "Pricing",
  description:
    "Simple, transparent pricing for agencies of every size. Start with SME Agency and scale to Enterprise.",
};

export default function PricingPage() {
  return (
    <section className="py-16 sm:py-20">
      <div className="container">
        <div className="mx-auto max-w-3xl text-center">
          <h1 className="text-4xl font-display font-extrabold tracking-tight text-white sm:text-5xl">
            Pricing built for every stage
          </h1>
          <p className="mt-4 text-lg text-slate-300">
            Start small, scale fast. All plans include our core CRM, listings
            and brochure automation.
          </p>
        </div>

        <div className="mt-10">
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

        <div className="mt-16 grid gap-8 lg:grid-cols-3">
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
                  R{plan.monthlyPrice}
                </span>
                <span className="text-sm text-slate-400">/month</span>
                <p className="mt-1 text-xs text-slate-400">
                  or R{plan.annualPrice}/year (save 2 months)
                </p>
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
                  href="/contact"
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

        <div className="mt-16 text-center">
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
