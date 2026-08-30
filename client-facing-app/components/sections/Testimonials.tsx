import { testimonials } from "@/lib/content";

export default function Testimonials() {
  return (
    <section className="py-16 sm:py-20">
      <div className="container">
        <div className="mx-auto max-w-3xl text-center">
          <h2 className="text-3xl font-display font-extrabold tracking-tight text-slate-900 sm:text-4xl">
            What our brokers say
          </h2>
        </div>

        <div className="mt-12 mx-auto grid max-w-5xl gap-6 lg:grid-cols-2">
          {testimonials.map((t) => (
            <div
              key={t.name}
              className="rounded-2xl border border-slate-200/70 bg-white p-6 shadow-card"
            >
              <p className="text-slate-700">“{t.quote}”</p>
              <div className="mt-4 flex items-center gap-3">
                <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-blue-100 text-blue-700 font-semibold">
                  {t.logo}
                </div>
                <div>
                  <p className="text-sm font-semibold text-slate-900">{t.name}</p>
                  <p className="text-sm text-slate-500">{t.role}</p>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
