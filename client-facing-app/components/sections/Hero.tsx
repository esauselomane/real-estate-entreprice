"use client";

import { useState, useEffect } from "react";
import { ArrowRight, Play, ChevronLeft, ChevronRight } from "lucide-react";
import Link from "next/link";
import { hero, heroSlides } from "@/lib/content";
import VideoModal from "@/components/ui/VideoModal";

export default function Hero() {
  const [showVideo, setShowVideo] = useState(false);
  const [current, setCurrent] = useState(0);

  useEffect(() => {
    const timer = setInterval(() => {
      setCurrent((prev) => (prev + 1) % heroSlides.length);
    }, 5000);
    return () => clearInterval(timer);
  }, []);

  const prev = () => setCurrent((prev) => (prev - 1 + heroSlides.length) % heroSlides.length);
  const next = () => setCurrent((prev) => (prev + 1) % heroSlides.length);

  return (
    <section className="relative isolate overflow-hidden pt-4 pb-16 sm:pt-8 sm:pb-20">
      <div
        aria-hidden="true"
        className="absolute -top-24 -left-24 h-80 w-80 rounded-full bg-blue-600/5 blur-3xl"
      />
      <div
        aria-hidden="true"
        className="absolute -bottom-24 -right-24 h-80 w-80 rounded-full bg-emerald-500/5 blur-3xl"
      />

      <div className="container relative z-10 grid items-center gap-12 lg:grid-cols-2 lg:py-8">
        <div>
          {hero.badge && (
            <span className="inline-flex rounded-full bg-emerald-50 px-3 py-1 text-xs font-extrabold text-emerald-700 ring-1 ring-emerald-200">
              {hero.badge}
            </span>
          )}
          <h1 className="mt-4 text-4xl font-display font-extrabold tracking-tight text-slate-900 sm:text-5xl">
            {hero.headline}
          </h1>
          <p className="mt-5 text-lg text-slate-600">{hero.subhead}</p>
          {hero.punchline && (
            <p className="mt-2 text-base font-medium italic text-blue-700">
              {hero.punchline}
            </p>
          )}

          <div className="mt-8 flex flex-wrap gap-4">
            <Link href="/login">
              <button
                type="button"
                className="inline-flex items-center gap-2 rounded-xl bg-blue-600 px-6 py-3 text-base font-semibold text-white transition-colors hover:bg-blue-700 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600"
              >
                Login
                <ArrowRight className="h-5 w-5" />
              </button>
            </Link>
            <button
              type="button"
              className="inline-flex items-center gap-2 rounded-xl border border-slate-300 px-6 py-3 text-base font-semibold text-slate-700 transition-colors hover:bg-slate-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-slate-400"
              onClick={() => setShowVideo(true)}
            >
              <Play className="h-4 w-4" />
              Watch overview
            </button>
          </div>
        </div>

        <div
          aria-hidden="true"
          className="esau-div relative isolate self-start rounded-2xl bg-white p-4 shadow-card ring-1 ring-slate-200/60 lg:sticky lg:top-8"
        >
          <div className="relative overflow-hidden rounded-xl">
            {heroSlides.map((slide, index) => (
              <div
                key={index}
                className={`absolute inset-0 transition-opacity duration-700 ${
                  index === current ? "opacity-100" : "opacity-0"
                } ${slide.isClickable ? "cursor-pointer" : ""}`}
                onClick={() => {
                  if (slide.isClickable && slide.url) {
                    window.location.href = slide.url;
                  }
                }}
              >
                <img
                  src={slide.image}
                  alt={slide.description}
                  className="aspect-video w-full object-cover"
                />
                <div className="absolute inset-0 bg-gradient-to-t from-slate-900/80 via-slate-900/20 to-transparent" />

                  <div className="absolute inset-x-0 bottom-0 p-6 text-center">
                    <p className="text-sm text-slate-200">{slide.description}</p>
                  </div>
              </div>
            ))}

            <div className="aspect-video" />

            <button
              type="button"
              onClick={prev}
              className="absolute left-3 top-1/2 -translate-y-1/2 z-10 rounded-full bg-white/90 p-2 text-slate-700 shadow-lg transition-colors hover:bg-white"
              aria-label="Previous slide"
            >
              <ChevronLeft className="h-4 w-4" />
            </button>
            <button
              type="button"
              onClick={next}
              className="absolute right-3 top-1/2 -translate-y-1/2 z-10 rounded-full bg-white/90 p-2 text-slate-700 shadow-lg transition-colors hover:bg-white"
              aria-label="Next slide"
            >
              <ChevronRight className="h-4 w-4" />
            </button>
          </div>

          <div className="mt-4 flex justify-center gap-2">
            {heroSlides.map((_, index) => (
              <button
                key={index}
                type="button"
                onClick={() => setCurrent(index)}
                className={`h-2 rounded-full transition-all ${
                  index === current ? "w-6 bg-blue-600" : "w-2 bg-slate-300 hover:bg-slate-400"
                }`}
                aria-label={`Go to slide ${index + 1}`}
              />
            ))}
          </div>
        </div>
      </div>

      <VideoModal open={showVideo} onClose={() => setShowVideo(false)} />
    </section>
  );
}
