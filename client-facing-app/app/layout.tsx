import "./globals.css";
import type { Metadata } from "next";
import type { ReactNode } from "react";
import Header from "@/components/layout/Header";
import Footer from "@/components/layout/Footer";
import BibsyWidget from "@/components/chat/BibsyWidget";

export const metadata: Metadata = {
  title: {
    default: "Biscove Technologies",
    template: "%s | Biscove Technologies",
  },
  description:
    "Hosted business software for real estate operations and land administration.",
  keywords: [
    "Biscove Technologies",
    "land administration",
    "GIS software",
    "real estate CRM",
    "brokerage software",
    "property listings",
    "deal pipeline",
    "listing syndication",
    "South Africa",
  ],
  authors: [{ name: "Biscove Technologies" }],
  openGraph: {
    type: "website",
    locale: "en_ZA",
    url: "https://www.biscovetech.com",
    title: "Biscove Technologies",
    description: "Explore Biscove Real Estate and Biscove Land Administration.",
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
