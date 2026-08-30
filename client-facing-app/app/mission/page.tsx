import { impact, values } from "@/lib/content";
import IconByName from "@/components/ui/IconByName";

export const metadata = {
  title: "Mission & Values",
  description:
    "Our purpose, vision and the values that guide every feature we ship.",
};

export default function MissionPage() {
  return (
    <section className="py-16 sm:py-20">
      <div className="container">
        <div className="mx-auto max-w-3xl text-center">
          <h1 className="text-4xl font-display font-extrabold tracking-tight text-slate-900 sm:text-5xl">
            Mission & Values
          </h1>
        </div>

        <div className="mt-12 rounded-3xl bg-slate-50 p-8 text-center ring-1 ring-slate-200/70 sm:p-12">
          <h2 className="text-xl font-display font-bold text-slate-900">
            Our mission
          </h2>
          <blockquote className="mt-4 text-2xl font-medium italic text-slate-800">
            To empower real-estate brokers and agencies with a single, intelligent
            workspace that turns every deal stage into measurable progress — fast,
            transparent, and beautifully automated.
          </blockquote>
        </div>

        <div className="mt-12 rounded-3xl bg-gradient-to-br from-blue-600/5 to-emerald-500/5 p-8 text-center ring-1 ring-slate-200/70 sm:p-12">
          <h2 className="text-xl font-display font-bold text-slate-900">
            Our vision
          </h2>
          <p className="mt-4 text-slate-600">
            A world where brokers close more deals with less friction — by
            uniting every tool in the pipeline under one trusted interface.
          </p>
        </div>

        <div className="mt-16">
          <h2 className="text-2xl font-display font-bold text-slate-900">
            Our values
          </h2>
          <div className="mt-8 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
            {values.map((value) => (
              <div
                key={value.title}
                className="rounded-2xl border border-slate-200/70 bg-white p-6 shadow-card transition-transform hover:-translate-y-0.5"
              >
                <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-emerald-50 text-emerald-600">
                  <IconByName name={value.icon} className="h-5 w-5" />
                </div>
                <h3 className="mt-4 text-lg font-semibold text-slate-900">
                  {value.title}
                </h3>
                <p className="mt-2 text-sm text-slate-600">{value.description}</p>
              </div>
            ))}
          </div>
        </div>

        <div className="mt-16">
          <div className="grid grid-cols-2 gap-6 text-center sm:grid-cols-4">
            {impact.map((stat) => (
              <div
                key={stat.label}
                className="rounded-2xl border border-slate-200/70 bg-white py-6 shadow-card"
              >
                <span className="block text-3xl font-extrabold text-blue-600">
                  {stat.value}
                </span>
                <span className="mt-1 block text-sm text-slate-500">{stat.label}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}
