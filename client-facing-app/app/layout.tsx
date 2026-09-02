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
    "One platform for full-lifecycle real-estate deals: CRM, listings, automated branded brochures and market syndication for brokers and agencies.",
  keywords: [
    "real estate",
    "CRM",
    "brokers",
    "property listings",
    "brochure automation",
    "market syndication",
  ],
  authors: [{ name: "Biscove Real Estate Technology" }],
  openGraph: {
    type: "website",
    locale: "en_ZA",
    url: "https://realestate.example",
    title: "Biscove Real Estate Technology",
    description:
      "CRM, property listings, automated branded brochures and market syndication.",
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
