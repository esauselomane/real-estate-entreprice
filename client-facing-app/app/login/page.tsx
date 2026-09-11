"use client";

import { useEffect } from "react";
import Link from "next/link";

const APP_URL = process.env.NEXT_PUBLIC_APP_URL || "http://app.biscovetech.com";

export default function LoginRedirect() {
  useEffect(() => {
    window.location.href = APP_URL;
  }, []);

  return (
    <section className="flex min-h-[60vh] items-center justify-center py-20">
      <div className="container text-center">
        <h1 className="text-2xl font-display font-extrabold text-white">
          Redirecting to the broker portal…
        </h1>
        <p className="mt-3 text-slate-300">
          You will be taken to the application shortly.
        </p>
        <p className="mt-6">
          <Link
            href="/"
            className="text-sm font-medium text-blue-400 underline underline-offset-2 hover:text-blue-400"
          >
            Back to the marketing site
          </Link>
        </p>
      </div>
    </section>
  );
}
