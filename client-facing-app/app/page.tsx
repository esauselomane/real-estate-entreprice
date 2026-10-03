import Link from "next/link";
import { ArrowRight, Layers, ShieldCheck, Workflow } from "lucide-react";
import Solutions from "@/components/sections/Solutions";
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
      <section className="relative overflow-hidden bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 py-16 sm:py-24">
        <div className="container grid items-center gap-12 lg:grid-cols-2">
          <div>
            <p className="text-sm font-semibold uppercase tracking-widest text-blue-400">
              Biscove Technologies
            </p>
            <h1 className="mt-5 text-4xl font-display font-bold tracking-tight sm:text-6xl">
              Technology that brings your operations together.
            </h1>
            <p className="mt-6 max-w-xl text-lg text-slate-300">
              We build business software that helps organisations manage
              property, land, people and the decisions that connect them.
            </p>
            <div className="mt-8 flex flex-wrap gap-4">
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
          <div className="space-y-4 rounded-3xl border border-slate-700 bg-slate-800/50 p-6 sm:p-8">
            <p className="text-sm uppercase tracking-widest text-blue-300">
              One company. Purpose-built solutions.
            </p>
            {capabilities.map(({ title, text, icon: Icon }) => (
              <div
                key={title}
                className="flex gap-4 border-t border-slate-700 pt-5"
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
      <section className="container pb-16">
        <div className="peach-cta rounded-3xl p-8 sm:p-12">
          <h2 className="text-3xl font-display font-bold">
            Let’s understand your organisation’s next step.
          </h2>
          <p className="mt-4 max-w-2xl text-slate-200">
            Explore our products or speak with us about your team, your records
            and the workflows you need to manage.
          </p>
          <Link
            href="/contact"
            className="mt-6 inline-flex rounded-xl bg-white px-6 py-3 font-semibold text-slate-900"
          >
            Contact Biscove
          </Link>
        </div>
      </section>
    </>
  );
}
