"use client";

import type { FormEvent } from "react";
import { useState, useEffect } from "react";
import { Check } from "lucide-react";

type FormState = "idle" | "submitting" | "success" | "error";

export default function ContactForm() {
  const [state, setState] = useState<FormState>("idle");
  const [error, setError] = useState("");
  const [solution, setSolution] = useState("");
  useEffect(() => {
    const requested = new URLSearchParams(window.location.search).get(
      "solution",
    );
    if (requested === "real-estate" || requested === "land-administration")
      setSolution(requested);
  }, []);

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setError("");
    setState("submitting");

    const form = e.currentTarget;
    const formData = new FormData(form);

    const payload = {
      fullName: formData.get("name")?.toString().trim() || "",
      email: formData.get("email")?.toString().trim() || "",
      phone: formData.get("phone")?.toString().trim() || undefined,
      company: formData.get("company")?.toString().trim() || undefined,
      userType: formData.get("userType")?.toString() || "Individual",
      package: formData.get("package")?.toString() || undefined,
      demoRequested: formData.get("demoRequested") === "on",
      message:
        [
          solution ? `Solution: ${solution}` : "",
          formData.get("message")?.toString().trim(),
        ]
          .filter(Boolean)
          .join("\n") || undefined,
      source: "contact",
    };

    try {
      const res = await fetch("/api/leads", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });

      const data = await res.json();

      if (!res.ok) {
        throw new Error(data.message || "Failed to submit.");
      }

      setState("success");
      form.reset();
      setTimeout(() => setState("idle"), 5000);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Something went wrong.");
      setState("error");
    }
  };

  return (
    <form
      onSubmit={handleSubmit}
      className="grid gap-4 rounded-2xl border border-slate-200/70 bg-white p-6 shadow-card [color-scheme:light]"
    >
      {state === "success" && (
        <p className="flex items-center gap-2 rounded-lg bg-emerald-50 p-2 text-sm font-medium text-emerald-800">
          <Check className="h-4 w-4" />
          Message sent — we&apos;ll be in touch shortly.
        </p>
      )}
      {state === "error" && (
        <p className="rounded-lg bg-red-50 p-2 text-sm font-medium text-red-800">
          {error}
        </p>
      )}

      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label className="mb-1 block text-xs font-medium text-slate-600">
            Full name <span className="text-red-500">*</span>
          </label>
          <input
            required
            name="name"
            type="text"
            placeholder="Your full name"
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-600/30"
          />
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-slate-600">
            Email <span className="text-red-500">*</span>
          </label>
          <input
            required
            name="email"
            type="email"
            placeholder="you@example.com"
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-600/30"
          />
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label className="mb-1 block text-xs font-medium text-slate-600">
            Phone
          </label>
          <input
            name="phone"
            type="tel"
            placeholder="+27 82 000 0000"
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-600/30"
          />
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-slate-600">
            Company
          </label>
          <input
            name="company"
            type="text"
            placeholder="Organisation or company name"
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-600/30"
          />
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label className="mb-1 block text-xs font-medium text-slate-600">
            I am a...
          </label>
          <select
            name="userType"
            defaultValue="Individual"
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-600/30"
          >
            <option value="Individual">
              Individual / Organisation representative
            </option>
            <option value="Agency">Agency / Brokerage</option>
          </select>
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-slate-600">
            Interested solution
          </label>
          <select
            name="solution"
            value={solution}
            onChange={(event) => setSolution(event.target.value)}
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 focus:outline-none focus:ring-2 focus:ring-blue-600/30"
          >
            <option value="">Help me choose</option>
            <option value="real-estate">Biscove Real Estate</option>
            <option value="land-administration">
              Biscove Land Administration
            </option>
            <option value="both">Both solutions</option>
          </select>
        </div>
      </div>

      <div className="flex items-center gap-2">
        <input
          id="demoRequested"
          name="demoRequested"
          type="checkbox"
          className="h-4 w-4 rounded border-slate-300 text-blue-600 focus:ring-blue-600"
        />
        <label htmlFor="demoRequested" className="text-sm text-slate-700">
          I&apos;d like to schedule a demo
        </label>
      </div>

      <div>
        <label className="mb-1 block text-xs font-medium text-slate-600">
          Message
        </label>
        <textarea
          name="message"
          rows={4}
          placeholder="Tell us about your needs..."
          className="w-full resize-y rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-600/30"
        />
      </div>

      <button
        type="submit"
        disabled={state === "submitting"}
        className="rounded-xl bg-blue-600 px-6 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-blue-700 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 disabled:opacity-50"
      >
        {state === "submitting" ? "Sending..." : "Send message"}
      </button>
    </form>
  );
}
