import Link from "next/link";
import { impact } from "@/lib/content";

export default function CtaStripe() {
  return (
    <section className="py-16 sm:py-20">
      <div className="container">
        <div className="rounded-3xl bg-gradient-to-br from-blue-600 to-emerald-500 px-6 py-12 text-center shadow-card sm:px-12 sm:py-16">
          <h2 className="text-3xl font-display font-extrabold text-white sm:text-4xl">
            Ready to close faster?
          </h2>
          <p className="mt-4 max-w-2xl text-lg text-white/90">
            Join brokers who reduced brochure turnaround from hours to seconds —
            and saved thousands on hosting and IT overhead.
          </p>

          <div className="mt-10 flex flex-wrap justify-center gap-4">
            {impact.map((stat) => (
              <div
                key={stat.label}
                className="flex flex-col items-center justify-center rounded-xl bg-white/15 px-5 py-3"
              >
                <span className="text-2xl font-extrabold text-white">{stat.value}</span>
                <span className="text-sm text-white/80">{stat.label}</span>
              </div>
            ))}
          </div>

          <div className="mt-10">
            <Link href="/login">
              <button
                type="button"
                className="rounded-xl bg-white px-7 py-3 text-base font-semibold text-blue-600 shadow-lg transition-colors hover:bg-slate-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
              >
                Broker login
              </button>
            </Link>
          </div>
        </div>
      </div>
    </section>
  );
}
