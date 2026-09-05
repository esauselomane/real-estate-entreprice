import { ArrowRight } from "lucide-react";
import Link from "next/link";

const steps = [
  {
    step: "01",
    title: "Manage your pipeline",
    description:
      "Track leads, brokers and deals from first contact to signature in a unified CRM.",
  },
  {
    step: "02",
    title: "Generate branded output",
    description:
      "Turn a curated card into a print-ready PDF brochure, rendered automatically.",
  },
  {
    step: "03",
    title: "Close faster",
    description:
      "Syndicate listings and advance deals with full auditability and no manual steps.",
  },
];

export default function ValueProp() {
  return (
    <section className="py-16 sm:py-20">
      <div className="container">
        <div className="mx-auto max-w-3xl text-center">
          <h2 className="text-3xl font-display font-extrabold tracking-tight text-white sm:text-4xl">
            A workflow built for brokers
          </h2>
          <p className="mt-4 text-lg text-slate-300">
            Three steps from pipeline to signed deal.
          </p>
        </div>

        <div className="mt-12 grid gap-8 md:grid-cols-3">
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

        <div className="mt-12 text-center">
          <Link href="/login">
            <button
              type="button"
              className="inline-flex items-center gap-2 rounded-xl bg-emerald-500 px-6 py-3 text-base font-semibold text-slate-900 transition-all duration-200 ease-in-out hover:scale-105 hover:bg-emerald-400 hover:shadow-lg active:scale-95 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-400"
            >
              Login
              <ArrowRight className="h-5 w-5" />
            </button>
          </Link>
        </div>
      </div>
    </section>
  );
}
