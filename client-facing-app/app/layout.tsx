import "./globals.css";
import type { Metadata } from "next";
import type { ReactNode } from "react";
import Header from "@/components/layout/Header";
import Footer from "@/components/layout/Footer";
import BibsyWidget from "@/components/chat/BibsyWidget";

export const metadata: Metadata = {
  title: {
    default: "Biscove Real Estate Technology",
    template: "%s | Biscove Real Estate Technology",
  },
  description:
    "Hosted workspace for property inventory, leads and deals, CRM, reports, brochures, and listing distribution.",
  keywords: [
    "real estate CRM",
    "brokerage software",
    "property listings",
    "deal pipeline",
    "listing syndication",
    "South Africa",
  ],
  authors: [{ name: "Biscove Real Estate Technology" }],
  openGraph: {
    type: "website",
    locale: "en_ZA",
    url: "https://www.biscovetech.com",
    title: "Biscove Real Estate Technology",
    description:
      "Hosted workspace for inventory, pipeline, people, presentations, and agency administration.",
  },
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en" className="scroll-smooth">
      <body className="flex min-h-screen flex-col bg-slate-900 text-slate-100">
        <Header />
        <main className="flex-1">{children}</main>
        <Footer />
        <BibsyWidget />
      </body>
    </html>
  );
}
