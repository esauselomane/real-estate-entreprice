import { features } from "@/lib/content";
import IconByName from "@/components/ui/IconByName";

export default function ProductFeatures() {
  return (
    <section className="py-16 sm:py-20">
      <div className="container">
        <div className="mx-auto max-w-3xl text-center">
          <h2 className="text-3xl font-display font-extrabold tracking-tight text-white sm:text-4xl">
            Everything your deal pipeline needs
          </h2>
          <p className="mt-4 text-lg text-slate-300">
            Orchestrated CRM, listings and branded output in a single workspace.
          </p>
        </div>

        <div className="mt-12 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {features.map((feature) => (
            <div
              key={feature.title}
              className="group rounded-2xl border border-slate-700 bg-slate-800/60 p-6 shadow-lg shadow-black/20 transition-transform hover:-translate-y-0.5"
            >
              <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-blue-900/40 text-blue-400 transition-colors group-hover:bg-blue-900/60">
                <IconByName name={feature.icon} className="h-5 w-5" />
              </div>
              <h3 className="mt-4 text-lg font-semibold text-white">
                {feature.title}
              </h3>
              <p className="mt-2 text-sm text-slate-300">{feature.description}</p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
