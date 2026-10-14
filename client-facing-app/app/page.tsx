import Solutions from "@/components/sections/Solutions";
import Link from "next/link";
import { ArrowRight, Layers, ShieldCheck, Workflow } from "lucide-react";
export const metadata = {
  title: "Biscove Technologies",
  description:
    "Biscove Technologies builds hosted business software for real estate operations and land administration.",
};
const capabilities = [
  {
    title: "Connected business software",
    text: "Bring records, people and operational workflows together in hosted workspaces.",
    icon: Layers,
  },
  {
    title: "Control across organisations",
    text: "Give teams access according to their responsibilities, organisation and authorised areas.",
    icon: ShieldCheck,
  },
  {
    title: "Workflows with visibility",
    text: "Track activity from enquiry or application through review, decisions and reporting.",
    icon: Workflow,
  },
];
export default function HomePage() {
  return (
    <>
      <section className="relative overflow-hidden bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 py-8 sm:py-10">
        <div className="container grid items-center gap-6 lg:grid-cols-2">
          <div>
            <p className="text-sm font-semibold uppercase tracking-widest text-blue-400">
              Biscove Technologies
            </p>
            <h1 className="mt-5 text-4xl font-display font-bold tracking-tight sm:text-4xl">
              Real estate. Land administration. A clearer way to work.
            </h1>
            <p className="mt-4 max-w-xl text-lg text-slate-300">
              Purpose-built software for property teams and land authorities.
              Keep your records, people and decisions connected — from the first enquiry to the final allocation.
            </p>
            <div className="mt-5 flex flex-wrap gap-4">
              <Link
                href="/solutions"
                className="inline-flex items-center gap-2 rounded-xl bg-blue-600 px-6 py-3 font-semibold text-white"
              >
                Explore our solutions
                <ArrowRight size={18} />
              </Link>
              <Link
                href="/contact"
                className="rounded-xl border border-slate-600 px-6 py-3 font-semibold"
              >
                Talk to Biscove
              </Link>
            </div>
          </div>
          <div className="space-y-4 border-l border-slate-700 pl-6 lg:pl-10">
            <p className="text-sm uppercase tracking-widest text-blue-300">
              Built for your organisation
            </p>
            {capabilities.map(({ title, text, icon: Icon }) => (
              <div
                key={title}
                className="flex gap-4 border-t border-slate-700 pt-3"
              >
                <Icon className="h-7 w-7 shrink-0 text-emerald-400" />
                <div>
                  <h2 className="text-lg font-semibold">{title}</h2>
                  <p className="mt-2 text-sm leading-relaxed text-slate-300">
                    {text}
                  </p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>
      <Solutions />
    </>
  );
}
