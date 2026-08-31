"use client";

import type { FormEvent } from "react";
import { useState } from "react";
import { Check, Loader2 } from "lucide-react";
import Link from "next/link";

type FormState = "idle" | "submitting" | "success" | "error";

export default function OnboardingPage() {
  const [state, setState] = useState<FormState>("idle");
  const [error, setError] = useState("");

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setError("");
    setState("submitting");

    const form = e.currentTarget;
    const formData = new FormData(form);

    const payload = {
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
    <div className="min-h-screen bg-slate-50 py-12">
      <div className="mx-auto max-w-2xl">
        <div className="mb-8 text-center">
          <h1 className="text-3xl font-bold text-slate-900">
            Complete Your Onboarding
          </h1>
          <p className="mt-2 text-slate-600">
            Set up your company profile to get started.
          </p>
        </div>

        <form
          onSubmit={handleSubmit}
          className="space-y-6 rounded-2xl border border-slate-200/70 bg-white p-8 shadow-card"
        >
          {state === "success" && (
            <div className="flex items-center gap-2 rounded-lg bg-emerald-50 p-4 text-sm font-medium text-emerald-800">
              <Check className="h-5 w-5" />
              <span>Onboarding submitted — check your email to set your password.</span>
            </div>
          )}
          {state === "error" && (
            <div className="rounded-lg bg-red-50 p-4 text-sm font-medium text-red-800">
              {error}
            </div>
          )}

          <div className="grid gap-6 sm:grid-cols-2">
            <div className="sm:col-span-2">
              <label className="mb-1 block text-xs font-medium text-slate-600">
                Company / Agency Name <span className="text-red-500">*</span>
              </label>
              <input
                required
                name="companyName"
                type="text"
                placeholder="Your company name"
                disabled={state === "submitting"}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-blue-600/30 disabled:cursor-not-allowed disabled:opacity-50"
              />
            </div>

            <div className="sm:col-span-2">
              <label className="mb-1 block text-xs font-medium text-slate-600">
                Trading Name
              </label>
              <input
                name="tradingName"
                type="text"
                placeholder="Trading / doing-business-as name"
                disabled={state === "submitting"}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-blue-600/30 disabled:cursor-not-allowed disabled:opacity-50"
              />
            </div>

            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">
                Registration Number
              </label>
              <input
                name="registrationNumber"
                type="text"
                placeholder="e.g. 2016/123456"
                disabled={state === "submitting"}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-blue-600/30 disabled:cursor-not-allowed disabled:opacity-50"
              />
            </div>

            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">
                Contact Email <span className="text-red-500">*</span>
              </label>
              <input
                required
                name="contactEmail"
                type="email"
                placeholder="admin@yourcompany.com"
                disabled={state === "submitting"}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-blue-600/30 disabled:cursor-not-allowed disabled:opacity-50"
              />
            </div>

            <div className="sm:col-span-2">
              <label className="mb-1 block text-xs font-medium text-slate-600">
                Headquarters Address
              </label>
              <textarea
                name="headquartersAddress"
                rows={3}
                placeholder="123 Main Street, City, Country"
                disabled={state === "submitting"}
                className="w-full resize-y rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-blue-600/30 disabled:cursor-not-allowed disabled:opacity-50"
              />
            </div>

            <div className="sm:col-span-2 border-t border-slate-200 pt-4">
              <h3 className="text-sm font-semibold text-slate-900 mb-3">
                Primary Admin
              </h3>
            </div>

            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">
                Full Name
              </label>
              <input
                name="adminFullName"
                type="text"
                placeholder="Jane Doe"
                disabled={state === "submitting"}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-blue-600/30 disabled:cursor-not-allowed disabled:opacity-50"
              />
            </div>

            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">
                Email <span className="text-red-500">*</span>
              </label>
              <input
                required
                name="adminEmail"
                type="email"
                placeholder="jane@yourcompany.com"
                disabled={state === "submitting"}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-blue-600/30 disabled:cursor-not-allowed disabled:opacity-50"
              />
            </div>
          </div>

          <button
            type="submit"
            disabled={state === "submitting" || state === "success"}
            className="w-full rounded-xl bg-blue-600 px-6 py-3 text-sm font-semibold text-white transition-colors hover:bg-blue-700 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 disabled:opacity-50"
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

        <div className="mt-6 text-center text-sm text-slate-600">
          Already have access?{" "}
          <Link href="/" className="text-blue-600 hover:underline">
            Back to home
          </Link>
        </div>
      </div>
    </div>
  );
}
