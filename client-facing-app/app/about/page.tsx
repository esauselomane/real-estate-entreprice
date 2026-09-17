import { leadership, timeline } from "@/lib/content";
import IconByName from "@/components/ui/IconByName";

const capabilities = [
  { name: "Properties", icon: "Building2" },
  { name: "Leads & deals", icon: "Briefcase" },
  { name: "CRM", icon: "Users" },
  { name: "Reports", icon: "BarChart3" },
  { name: "Brochures", icon: "FileText" },
  { name: "Administration", icon: "Shield" },
];

export const metadata = {
  title: "About",
  description:
    "Biscove Real Estate Technology builds a hosted workspace for South African brokerages.",
};

export default function AboutPage() {
  return (
    <section className="py-10 sm:py-12">
      <div className="container">
        <div className="mx-auto max-w-3xl text-center">
          <h1 className="text-4xl font-display font-extrabold tracking-tight text-white sm:text-5xl">
            About Biscove
          </h1>
          <p className="mt-3 text-lg text-slate-300">
            We build software for agencies that need inventory, pipeline, people, and administration in one place — without running their own infrastructure.
          </p>
        </div>

        <div className="mt-10">
          <h2 className="text-2xl font-display font-bold text-white">
            History
          </h2>
          <div className="mt-6 space-y-5 border-l border-slate-700 pl-6">
            {timeline.map((item) => (
              <div key={item.year} className="relative -left-1.5 border-l border-transparent pl-6">
                <span className="inline-block rounded-full bg-blue-600 px-2.5 py-0.5 text-xs font-extrabold text-white">
                  {item.year}
                </span>
                <p className="mt-1 text-slate-300">{item.event}</p>
              </div>
            ))}
          </div>
        </div>

        <div className="mt-10">
          <h2 className="text-2xl font-display font-bold text-white">
            Workspace coverage
          </h2>
          <div className="mt-5 flex flex-wrap items-center justify-center gap-3">
            {capabilities.map((item) => (
              <div
                key={item.name}
                className="flex items-center gap-2 rounded-xl border border-slate-700 bg-slate-800/60 px-4 py-2"
              >
                <IconByName name={item.icon} className="h-4 w-4 text-blue-400" />
                <span className="text-sm font-medium text-slate-200">{item.name}</span>
              </div>
            ))}
          </div>
        </div>

        <div className="mt-10">
          <h2 className="text-2xl font-display font-bold text-white">
            Leadership
          </h2>
          <div className="mt-6 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
            {leadership.map((person) => (
              <a
                key={person.name}
                href={person.linkedin}
                target="_blank"
                rel="noreferrer"
                className="rounded-2xl border border-slate-700 bg-slate-800/60 p-6 text-center shadow-lg shadow-black/20 transition-colors hover:border-slate-500"
              >
                <div className="mx-auto mb-3 h-20 w-20 shrink-0 rounded-full bg-slate-700" />
                <h3 className="text-lg font-semibold text-white">{person.name}</h3>
                <p className="text-sm text-slate-400">{person.role}</p>
              </a>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}
