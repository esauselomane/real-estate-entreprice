import { testimonials } from "@/lib/content";

export default function Testimonials() {
  return (
    <section className="py-16 sm:py-20">
      <div className="container">
        <div className="mx-auto max-w-3xl text-center">
          <h2 className="text-3xl font-display font-extrabold tracking-tight text-white sm:text-4xl">
            What our brokers say
          </h2>
        </div>

        <div className="mt-12 mx-auto grid max-w-5xl gap-6 lg:grid-cols-2">
          {testimonials.map((t) => (
            <div
              key={t.name}
              className="rounded-2xl border border-slate-700 bg-slate-800/60 p-6 shadow-lg shadow-black/20"
            >
              <p className="text-slate-300">"{t.quote}"</p>
              <div className="mt-4 flex items-center gap-3">
                <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-blue-900/40 text-blue-400 font-semibold">
                  {t.logo}
                </div>
                <div>
                  <p className="text-sm font-semibold text-white">{t.name}</p>
                  <p className="text-sm text-slate-400">{t.role}</p>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
