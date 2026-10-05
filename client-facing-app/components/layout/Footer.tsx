import Link from "next/link";
import { Twitter, Linkedin, Facebook, Instagram, ArrowUpRight } from "lucide-react";
import { site } from "@/lib/content";

const socialLinks = [
  { label: "Twitter / X", href: "https://x.com/biscovetech", icon: Twitter },
  { label: "LinkedIn", href: "https://www.linkedin.com/company/biscovetech/", icon: Linkedin },
  { label: "Facebook", href: "https://www.facebook.com/biscovetech", icon: Facebook },
  { label: "Instagram", href: "https://www.instagram.com/biscovetech/", icon: Instagram },
];

const footerLinks = [
  { label: "Home", href: "/" },
  { label: "Our Solutions", href: "/solutions" },
  { label: "About", href: "/about" },
  { label: "Mission", href: "/mission" },
  { label: "Contact", href: "/contact" },
  { label: "Product access", href: "/solutions" },
];

export default function Footer() {
  const year = new Date().getFullYear();
  return (
    <footer className="bg-slate-900/50">
      <div className="container py-5">
        <div className="flex flex-col gap-6 border-b border-slate-700 pb-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <p className="text-xs uppercase tracking-[.2em] text-blue-300">A little more connected</p>
            <h2 className="mt-3 text-3xl font-display sm:text-4xl">Good things grow together.</h2>
            <p className="mt-3 text-sm text-slate-300">Follow Biscove Technologies <span className="text-blue-300">@biscovetech</span></p>
          </div>
          <nav aria-label="Biscove Technologies social media" className="flex flex-wrap gap-3 lg:max-w-md">
            {socialLinks.map(({ label, href, icon: Icon }) => (
              <a key={label} href={href} target="_blank" rel="noopener noreferrer" className="social-link" aria-label={`Biscove Technologies on ${label} (opens in a new tab)`}>
                <Icon aria-hidden="true" size={18} /><span className="text-sm">{label}</span><ArrowUpRight aria-hidden="true" size={14} />
              </a>
            ))}
          </nav>
        </div>
      </div>
      <div className="container grid gap-5 py-5 md:grid-cols-3">
        <div>
          <div className="flex items-center gap-3 text-xl font-display font-extrabold text-blue-400">
            <img src="/favicon.jpg" alt="Biscove Technologies logo" className="h-7 w-auto" />
            Biscove Technologies
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
          <p className="mt-6 text-xs text-slate-500">
            &copy; {year} {site.name}. All rights reserved.
          </p>
        </div>
      </div>
    </footer>
  );
}
