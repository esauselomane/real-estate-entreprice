import Link from "next/link";
import { ArrowUpRight, Check } from "lucide-react";
import IconByName from "@/components/ui/IconByName";
import { solutions } from "@/lib/solutions";
export default function Solutions() {
  return (
    <section
      className="container py-12 sm:py-16"
      aria-labelledby="solutions-heading"
    >
      <p className="text-sm font-semibold uppercase tracking-widest text-blue-400">
        Our solutions
      </p>
      <h2
        id="solutions-heading"
        className="mt-3 text-3xl font-display font-bold sm:text-4xl"
      >
        Purpose-built for the work you do.
      </h2>
      <p className="mt-4 max-w-2xl text-slate-300">
        Two distinct products from Biscove Technologies. Choose the workspace
        that fits your organisation.
      </p>
      <div className="mt-8 grid gap-6 lg:grid-cols-2">
        {solutions.map((solution) => (
          <article
            key={solution.id}
            id={solution.id}
            className="flex flex-col rounded-3xl border border-slate-700 bg-slate-800/50 p-7 sm:p-9"
          >
            <IconByName
              name={solution.icon}
              className="h-9 w-9 text-blue-400"
            />
            <p className="mt-5 text-sm text-blue-300">{solution.audience}</p>
            <h3 className="mt-2 text-2xl font-display font-bold">
              {solution.name}
            </h3>
            <p className="mt-4 text-slate-300">{solution.description}</p>
            <ul className="my-6 space-y-3 text-sm text-slate-300">
              {solution.features.map((feature) => (
                <li key={feature} className="flex gap-3">
                  <Check className="h-5 w-5 shrink-0 text-emerald-400" />
                  {feature}
                </li>
              ))}
            </ul>
            <Link
              href={solution.href}
              className="mt-auto inline-flex items-center gap-2 font-semibold text-blue-300"
            >
              {solution.action}
              <ArrowUpRight className="h-4 w-4" />
            </Link>
          </article>
        ))}
      </div>
    </section>
  );
}
