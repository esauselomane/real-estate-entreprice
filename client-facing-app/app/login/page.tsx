"use client";

import { useEffect } from "react";
import Link from "next/link";

const APP_URL = process.env.NEXT_PUBLIC_APP_URL || "http://localhost:5173";

export default function LoginRedirect() {
  useEffect(() => {
    window.location.href = APP_URL;
  }, []);

  return (
    <section className="flex min-h-[60vh] items-center justify-center py-20">
      <div className="container text-center">
        <h1 className="text-2xl font-display font-extrabold text-slate-900">
          Redirecting to the broker portal…
        </h1>
        <p className="mt-3 text-slate-600">
          You will be taken to the application shortly.
        </p>
        <p className="mt-6">
          <Link
            href="/"
            className="text-sm font-medium text-blue-600 underline underline-offset-2"
          >
            Back to the marketing site
          </Link>
        </p>
      </div>
    </section>
  );
}
