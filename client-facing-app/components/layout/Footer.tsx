import Link from "next/link";
import { site } from "@/lib/content";

const footerLinks = [
  { label: "Home", href: "/" },
  { label: "Pricing", href: "/pricing" },
  { label: "About", href: "/about" },
  { label: "Mission", href: "/mission" },
  { label: "Contact", href: "/contact" },
  { label: "Login", href: "/login" },
];

export default function Footer() {
  const year = new Date().getFullYear();
  return (
    <footer className="border-t border-slate-700 bg-slate-900/50">
      <div className="container grid gap-10 py-12 md:grid-cols-3">
        <div>
          <div className="flex items-center gap-3 text-xl font-display font-extrabold text-blue-400">
            <img src="/favicon.jpg" alt="Biscove Real Estate Tech logo" className="h-7 w-auto" />
            Biscove Real Estate Tech
          </div>
          <p className="mt-3 max-w-xs text-sm text-slate-400">{site.description}</p>
        </div>

        <div className="md:col-span-2">
          <nav
            className="flex flex-wrap gap-x-6 gap-y-2 text-sm"
            aria-label="Footer navigation"
          >
            {footerLinks.map((link) => (
              <Link
                key={link.label}
                href={link.href}
                className="text-slate-400 transition-colors hover:text-blue-400"
              >
                {link.label}
              </Link>
            ))}
          </nav>
          <p className="mt-8 text-xs text-slate-500">
            &copy; {year} {site.name}. All rights reserved.
          </p>
          <p className="mt-2 text-xs font-medium italic text-slate-400">
            We align the code, you sell the abode.
          </p>
        </div>
      </div>
    </footer>
  );
}
