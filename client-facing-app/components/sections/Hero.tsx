"use client";

import { useState } from "react";
import { ArrowRight, Play } from "lucide-react";
import Link from "next/link";
import { hero } from "@/lib/content";
import VideoModal from "@/components/ui/VideoModal";

export default function Home() {
  const [showVideo, setShowVideo] = useState(false);

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
                login
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
          className="relative isolate self-start rounded-2xl bg-white p-4 shadow-card ring-1 ring-slate-200/60 lg:sticky lg:top-8"
        >
          <div className="aspect-video rounded-xl bg-slate-100" />
          <div className="mt-4 space-y-2">
            <div className="h-3 w-3/4 rounded bg-slate-200" />
            <div className="h-3 w-1/2 rounded bg-slate-200" />
            <div className="h-3 w-5/6 rounded bg-slate-200" />
          </div>
        </div>
      </div>

      <VideoModal open={showVideo} onClose={() => setShowVideo(false)} />
    </section>
  );
}
