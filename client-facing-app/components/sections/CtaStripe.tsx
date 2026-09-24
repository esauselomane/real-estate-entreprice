import Link from "next/link";
import { impact } from "@/lib/content";

export default function CtaStripe() {
  return (
    <section className="py-10 sm:py-12">
      <div className="container">
        <div className="peach-cta px-6 py-8 text-center shadow-card sm:px-10 sm:py-10">
          <h2 className="text-3xl font-display font-extrabold text-white sm:text-4xl">
            See the workspace with your own stock
          </h2>
          <p className="mx-auto mt-3 max-w-2xl text-base text-white/90 sm:text-lg">
            Start a trial or speak with us about onboarding your agency, listings, and team.
          </p>

          <div className="mt-6 flex flex-wrap justify-center gap-3">
            {impact.map((stat) => (
              <div
                key={stat.label}
                className="flex min-w-[9rem] flex-col items-center justify-center rounded-xl bg-white/15 px-4 py-2.5"
              >
                <span className="text-lg font-extrabold text-white">{stat.value}</span>
                <span className="text-xs text-white/80">{stat.label}</span>
              </div>
            ))}
          </div>

          <div className="mt-8 flex flex-wrap justify-center gap-3">
            <Link href="/onboarding">
              <button
                type="button"
                className="rounded-xl bg-white px-7 py-3 text-base font-semibold text-blue-600 shadow-lg transition-all duration-200 ease-in-out hover:scale-105 hover:bg-slate-100 hover:shadow-lg active:scale-95 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
              >
                Start a trial
              </button>
            </Link>
            <Link href="/contact">
              <button
                type="button"
                className="rounded-xl border border-[#805137] bg-[#492e22] px-7 py-3 text-base font-semibold text-white transition-all duration-200 ease-in-out hover:bg-[#65402f]"
              >
                Contact us
              </button>
            </Link>
          </div>
        </div>
      </div>
    </section>
  );
}
