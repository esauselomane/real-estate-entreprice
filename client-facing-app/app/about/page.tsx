import { leadership, timeline } from "@/lib/content";
import IconByName from "@/components/ui/IconByName";

const stack = [
  { name: "Identity", icon: "Shield" },
  { name: "Gateway", icon: "Globe" },
  { name: "CRM", icon: "UserCheck" },
  { name: "Property", icon: "Building2" },
  { name: "Workers", icon: "Bot" },
];

export const metadata = {
  title: "About",
  description:
    "The story behind Biscove Real Estate Technology — built for brokers, by people who understand deal flow.",
};

export default function AboutPage() {
  return (
    <section className="py-16 sm:py-20">
      <div className="container">
        <div className="mx-auto max-w-3xl text-center">
          <h1 className="text-4xl font-display font-extrabold tracking-tight text-white sm:text-5xl">
            About Biscove Real Estate Technology
          </h1>
          <p className="mt-4 text-lg text-slate-300">
            We build software that lets brokers spend less time on paperwork and
            more time closing deals.
          </p>
        </div>

        <div className="mt-16">
          <h2 className="text-2xl font-display font-bold text-white">
            Our story
          </h2>
          <div className="mt-8 space-y-6 border-l border-slate-700 pl-6">
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

        <div className="mt-16">
          <h2 className="text-2xl font-display font-bold text-white">
            The platform
          </h2>
          <div className="mt-6 flex flex-wrap items-center justify-center gap-3">
            {stack.map((s) => (
              <div
                key={s.name}
                className="flex items-center gap-2 rounded-xl border border-slate-700 bg-slate-800/60 px-4 py-2"
              >
                <IconByName name={s.icon} className="h-4 w-4 text-blue-400" />
                <span className="text-sm font-medium text-slate-200">{s.name}</span>
              </div>
            ))}
          </div>
        </div>

        <div className="mt-16">
          <h2 className="text-2xl font-display font-bold text-white">
            Leadership
          </h2>
          <div className="mt-8 grid gap-8 sm:grid-cols-2 lg:grid-cols-3">
            {leadership.map((person) => (
              <div
                key={person.name}
                className="rounded-2xl border border-slate-700 bg-slate-800/60 p-6 text-center shadow-lg shadow-black/20"
              >
                <div className="mx-auto mb-3 h-20 w-20 shrink-0 rounded-full bg-slate-700" />
                <h3 className="text-lg font-semibold text-white">{person.name}</h3>
                <p className="text-sm text-slate-400">{person.role}</p>
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}
