"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Menu, X } from "lucide-react";
import { navigation } from "@/lib/content";

export default function Header() {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  return (
    <header className="sticky top-0 z-40 w-full bg-white/80 shadow-sm ring-1 ring-slate-200/60 backdrop-blur">
      <nav
        className="container flex h-16 items-center justify-between"
        aria-label="Main navigation"
      >
        <Link href="/" className="flex items-center gap-3 text-xl font-display font-extrabold text-blue-600">
          <img src="/logo.svg" alt="Biscove Real Estate Tech logo" className="h-8 w-auto" />
          Biscove Real Estate Tech
        </Link>

        <div className="hidden items-center gap-8 md:flex">
          {navigation.map((item) => (
            <Link
              key={item.label}
              href={item.href}
              className="text-sm font-medium text-slate-700 transition-colors hover:text-blue-600"
            >
              {item.label}
            </Link>
          ))}
          <Link
            href="/login"
            className="rounded-xl bg-blue-600 px-5 py-2 text-sm font-semibold text-white transition-colors hover:bg-blue-700"
          >
            Login
          </Link>
        </div>

        <button
          type="button"
          className="md:hidden rounded-xl p-2 text-slate-700 hover:bg-slate-100"
          aria-label={open ? "Close menu" : "Open menu"}
          aria-expanded={open}
          onClick={() => setOpen(!open)}
        >
          {open ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
        </button>
      </nav>

      {open ? (
        <div className="md:hidden container pb-4">
          {navigation.map((item) => (
            <Link
              key={item.label}
              href={item.href}
              className="block py-2 text-sm font-medium text-slate-700 hover:text-blue-600"
              onClick={() => setOpen(false)}
            >
              {item.label}
            </Link>
          ))}
        </div>
      ) : null}
    </header>
  );
}
