import { impact, values } from "@/lib/content";
import IconByName from "@/components/ui/IconByName";

export const metadata = {
  title: "Mission & Values",
  description:
    "Why Biscove exists and the standards that shape our business software.",
};

export default function MissionPage() {
  return (
    <section className="py-6 sm:py-5">
      <div className="container">
        <div className="mx-auto max-w-3xl text-center">
          <h1 className="text-4xl font-display font-extrabold tracking-tight text-white sm:text-5xl">
            Mission &amp; values
          </h1>
        </div>

        <div className="mt-5 rounded-3xl bg-slate-800/60 p-6 text-center ring-1 ring-slate-700 sm:p-8">
          <h2 className="text-xl font-display font-bold text-white">Mission</h2>
          <blockquote className="mt-3 text-xl font-medium text-slate-200 sm:text-2xl">
            Give organisations clear, connected systems for managing their
            records, people and operational decisions.
          </blockquote>
        </div>

        <div className="mt-6 rounded-3xl bg-gradient-to-br from-blue-600/10 to-emerald-500/10 p-6 text-center ring-1 ring-slate-700 sm:p-8">
          <h2 className="text-xl font-display font-bold text-white">Vision</h2>
          <p className="mt-3 text-slate-300">
            Organisations that can manage property and land with reliable
            information, visible responsibilities and workflows their teams can
            understand.
          </p>
        </div>

        <div className="mt-6">
          <h2 className="text-2xl font-display font-bold text-white">Values</h2>
          <div className="mt-6 grid gap-5 sm:grid-cols-2">
            {values.map((value) => (
              <div
                key={value.title}
                className="rounded-2xl border border-slate-700 bg-slate-800/60 p-6 shadow-lg shadow-black/20"
              >
                <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-emerald-900/40 text-emerald-400">
                  <IconByName name={value.icon} className="h-5 w-5" />
                </div>
                <h3 className="mt-4 text-lg font-semibold text-white">
                  {value.title}
                </h3>
                <p className="mt-2 text-sm text-slate-300">
                  {value.description}
                </p>
              </div>
            ))}
          </div>
        </div>

        <div className="mt-6">
          <div className="grid grid-cols-2 gap-4 text-center sm:grid-cols-4">
            {impact.map((stat) => (
              <div
                key={stat.label}
                className="rounded-2xl border border-slate-700 bg-slate-800/60 px-3 py-5 shadow-lg shadow-black/20"
              >
                <span className="block text-lg font-extrabold text-blue-400">
                  {stat.value}
                </span>
                <span className="mt-1 block text-xs text-slate-400">
                  {stat.label}
                </span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}
