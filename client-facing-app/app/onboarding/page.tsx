"use client";

import type { FormEvent } from "react";
import { useState } from "react";
import { Check, Loader2, Mail } from "lucide-react";
import Link from "next/link";

type FormState = "idle" | "submitting" | "success" | "error";

export default function OnboardingPage() {
  const [state, setState] = useState<FormState>("idle");
  const [error, setError] = useState("");
  const [includeSampleData, setIncludeSampleData] = useState(false);

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setError("");
    setState("submitting");

    const form = e.currentTarget;
    const formData = new FormData(form);

    const payload = {
      includeSampleData,
      agencyName: formData.get("companyName")?.toString().trim() || "",
      tradingName: formData.get("tradingName")?.toString().trim() || "",
      registrationNumber: formData.get("registrationNumber")?.toString().trim() || "",
      contactEmail: formData.get("contactEmail")?.toString().trim() || "",
      headquartersAddress: formData.get("headquartersAddress")?.toString().trim() || "",
      adminFullName: formData.get("adminFullName")?.toString().trim() || "",
      adminEmail: formData.get("adminEmail")?.toString().trim() || "",
      adminPhone: formData.get("adminPhone")?.toString().trim() || "",
    };

    try {
      const res = await fetch("/api/onboarding", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });

      const data = await res.json();

      if (!res.ok) {
        throw new Error(data.message || "Onboarding failed.");
      }

      setState("success");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Something went wrong.");
      setState("error");
    }
  };

  return (
    <div className="bg-slate-900 py-8">
      <div className="mx-auto max-w-2xl">
        <div className="mb-8 text-center">
          <h1 className="text-3xl font-bold text-white">
            Start your Biscove Real Estate trial
          </h1>
          <p className="mt-2 text-slate-400">
            Set up your real estate agency and its administrator account.
          </p>
        </div>

        <div className="relative">
          <form
            onSubmit={handleSubmit}
            className={`space-y-6 rounded-2xl border border-slate-700 bg-slate-800/60 p-8 shadow-lg shadow-black/20 transition-opacity duration-300 ${
              state === "success" ? "pointer-events-none opacity-30" : ""
            }`}
          >
            {state === "error" && (
              <div className="rounded-lg bg-red-900/40 p-4 text-sm font-medium text-red-300">
                {error}
              </div>
            )}

            <label className="flex items-start gap-3 rounded-xl border border-blue-500/30 bg-blue-500/5 p-4">
              <input type="checkbox" name="includeSampleData" checked={includeSampleData}
                onChange={event => setIncludeSampleData(event.target.checked)} disabled={state === "submitting" || state === "success"}
                className="mt-1 accent-blue-500" />
              <span><span className="block text-sm font-semibold text-white">Request sample data</span>
                <span className="mt-1 block text-xs leading-5 text-slate-400">Explore a connected example with 5 contacts, 5 brokers, 5 nearby sample properties, 1 company, 1 legal entity, 5 leads and 5 deals. All records are fictional and belong only to your agency. Your address is used to locate the sample properties.</span>
              </span>
            </label>

            <div className="grid gap-6 sm:grid-cols-2">
              <div className="sm:col-span-2">
                <label className="mb-1 block text-xs font-medium text-slate-400">
                  Company / Agency Name <span className="text-red-500">*</span>
                </label>
                <input
                  required
                  name="companyName"
                  type="text"
                  placeholder="Your company name"
                  disabled={state === "submitting" || state === "success"}
                  className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white placeholder-slate-500 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/30 disabled:cursor-not-allowed disabled:opacity-50"
                />
              </div>

              <div className="sm:col-span-2">
                <label className="mb-1 block text-xs font-medium text-slate-400">
                  Trading Name
                </label>
                <input
                  name="tradingName"
                  type="text"
                  placeholder="Trading / doing-business-as name"
                  disabled={state === "submitting" || state === "success"}
                  className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white placeholder-slate-500 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/30 disabled:cursor-not-allowed disabled:opacity-50"
                />
              </div>

              <div>
                <label className="mb-1 block text-xs font-medium text-slate-400">
                  Registration Number
                </label>
                <input
                  name="registrationNumber"
                  type="text"
                  placeholder="e.g. 2016/123456"
                  disabled={state === "submitting" || state === "success"}
                  className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white placeholder-slate-500 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/30 disabled:cursor-not-allowed disabled:opacity-50"
                />
              </div>

              <div>
                <label className="mb-1 block text-xs font-medium text-slate-400">
                  Contact Email <span className="text-red-500">*</span>
                </label>
                <input
                  required
                  name="contactEmail"
                  type="email"
                  placeholder="admin@yourcompany.com"
                  disabled={state === "submitting" || state === "success"}
                  className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white placeholder-slate-500 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/30 disabled:cursor-not-allowed disabled:opacity-50"
                />
              </div>

              <div className="sm:col-span-2">
                <label className="mb-1 block text-xs font-medium text-slate-400">
                  Headquarters Address
                </label>
                <textarea
                  name="headquartersAddress"
                  required={includeSampleData}
                  rows={3}
                  placeholder="123 Main Street, City, Country"
                  disabled={state === "submitting" || state === "success"}
                  className="w-full resize-y rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white placeholder-slate-500 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/30 disabled:cursor-not-allowed disabled:opacity-50"
                />
              </div>

              <div className="sm:col-span-2 border-t border-slate-700 pt-4">
                <h3 className="text-sm font-semibold text-white mb-3">
                  Primary Admin
                </h3>
              </div>

              <div>
                <label className="mb-1 block text-xs font-medium text-slate-400">
                  Full Name
                </label>
                <input
                  name="adminFullName"
                  type="text"
                  placeholder="Jane Doe"
                  disabled={state === "submitting" || state === "success"}
                  className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white placeholder-slate-500 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/30 disabled:cursor-not-allowed disabled:opacity-50"
                />
              </div>

              <div>
                <label className="mb-1 block text-xs font-medium text-slate-400">
                  Email <span className="text-red-500">*</span>
                </label>
                <input
                  required
                  name="adminEmail"
                  type="email"
                  placeholder="jane@yourcompany.com"
                  disabled={state === "submitting" || state === "success"}
                  className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white placeholder-slate-500 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/30 disabled:cursor-not-allowed disabled:opacity-50"
                />
              </div>
            </div>

            <button
              type="submit"
              disabled={state === "submitting" || state === "success"}
              className="w-full rounded-xl bg-blue-600 px-6 py-3 text-sm font-semibold text-white transition-all duration-200 ease-in-out hover:scale-105 hover:bg-blue-500 hover:shadow-lg active:scale-95 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-500 disabled:opacity-50"
            >
              {state === "submitting" ? (
                <span className="flex items-center justify-center gap-2">
                  <Loader2 className="h-4 w-4 animate-spin" />
                  Submitting...
                </span>
              ) : (
                "Submit Onboarding"
              )}
            </button>
          </form>

          {state === "success" && (
            <div className="absolute inset-0 flex flex-col items-center justify-center rounded-2xl bg-slate-800/95 backdrop-blur-sm">
              <div className="relative flex h-24 w-24 items-center justify-center rounded-full border-4 border-slate-700 bg-slate-900">
                <Mail className="h-10 w-10 text-slate-400" />
                <div className="absolute bottom-2 right-2 flex h-7 w-7 items-center justify-center rounded-full bg-emerald-500 text-white">
                  <Check className="h-4 w-4" />
                </div>
              </div>
              <p className="mt-4 text-center text-sm font-medium text-slate-300">
                Your onboarding has been submitted successfully. {includeSampleData && "Your sample data request has been recorded. Our platform administrator will prepare your linked sample workspace, and we’ll email you when it is ready."}
              </p>
              <p className="mt-1 text-center text-xs text-slate-400">
                Check your email to set your password and activate your account.
              </p>
            </div>
          )}
        </div>

        <div className="mt-6 text-center text-sm text-slate-400">
          Already have access?{" "}
          <Link href="/" className="text-blue-400 hover:underline">
            Go to Login
          </Link>
        </div>
      </div>
    </div>
  );
}
