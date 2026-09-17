import { ArrowRight } from "lucide-react";
import Link from "next/link";

const steps = [
  {
    step: "01",
    title: "Hold the stock",
    description:
      "Catalogue properties, units, amenities, media, and documents. Find them by search or on the map.",
  },
  {
    step: "02",
    title: "Run the pipeline",
    description:
      "Work leads, assign brokers, manage deals, and keep owners, tenants, and company contacts in the CRM.",
  },
  {
    step: "03",
    title: "Present and govern",
    description:
      "Issue pitch packs, syndicate listings, review performance, and administer roles, agencies, and billing.",
  },
];

export default function ValueProp() {
  return (
    <section className="py-10 sm:py-12">
      <div className="container">
        <div className="mx-auto max-w-3xl text-center">
          <h2 className="text-3xl font-display font-extrabold tracking-tight text-white sm:text-4xl">
            How agencies use Biscove
          </h2>
          <p className="mt-3 text-base text-slate-300 sm:text-lg">
            The same capabilities your team uses in the workspace, summarised for leadership.
          </p>
        </div>

        <div className="mt-8 grid gap-6 md:grid-cols-3">
          {steps.map((step) => (
            <div key={step.step} className="flex gap-4">
              <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-blue-600 text-sm font-extrabold text-white">
                {step.step}
              </div>
              <div>
                <h3 className="text-lg font-semibold text-white">{step.title}</h3>
                <p className="mt-1 text-sm text-slate-300">{step.description}</p>
              </div>
            </div>
          ))}
        </div>

        <div className="mt-8 text-center">
          <Link href="/login">
            <button
              type="button"
              className="inline-flex items-center gap-2 rounded-xl bg-emerald-500 px-6 py-3 text-base font-semibold text-slate-900 transition-all duration-200 ease-in-out hover:scale-105 hover:bg-emerald-400 hover:shadow-lg active:scale-95 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-400"
            >
              Open the workspace
              <ArrowRight className="h-5 w-5" />
            </button>
          </Link>
        </div>
      </div>
    </section>
  );
}
