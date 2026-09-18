"use client";

import { useEffect, useRef, useState } from "react";
import { Bot, MessageCircle, Send, X, User, Mail, Phone, Building2, Rocket } from "lucide-react";
import { findAnswer, getApiHint } from "@/lib/bibsyKnowledge";

type Message = { role: "bot" | "user"; text: string };
type ConversationState =
  | { step: "idle" }
  | { step: "type" }
  | { step: "name" }
  | { step: "email" }
  | { step: "phone" }
  | { step: "company" }
  | { step: "demo" }
  | { step: "callback" }
  | { step: "done" };

type LeadDraft = {
  userType: "Agency" | "Individual";
  fullName: string;
  email: string;
  phone: string;
  company: string;
  demoRequested: boolean;
  callbackRequested: boolean;
  message: string;
};

const STORAGE_KEY = "bibsy-taught";
const MAX_TAUGHT = 50;
const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const teachRegex = /^(learn|teach)\s*[:\-]?\s*(.+?)\s*[:=]\s*(.+)$/i;

export default function BibsyWidget() {
  const [open, setOpen] = useState(false);
  const [messages, setMessages] = useState<Message[]>([
    {
      role: "bot",
      text: "Hi! I'm Bibsy 🤖 — your real-estate assistant. I can help you onboard a new agency, answer questions about the platform, or request a demo. What brings you here today?",
    },
  ]);
  const [input, setInput] = useState("");
  const [taught, setTaught] = useState<Record<string, string>>({});
  const [state, setState] = useState<ConversationState>({ step: "idle" });
  const [draft, setDraft] = useState<LeadDraft>({
    userType: "Individual",
    fullName: "",
    email: "",
    phone: "",
    company: "",
    demoRequested: false,
    callbackRequested: false,
    message: "",
  });
  const [submitting, setSubmitting] = useState(false);
  const listRef = useRef<HTMLUListElement>(null);

  useEffect(() => {
    try {
      setTaught(JSON.parse(localStorage.getItem(STORAGE_KEY) || "{}"));
    } catch {}
  }, []);

  useEffect(() => {
    listRef.current?.scrollTo({
      top: listRef.current.scrollHeight,
      behavior: "smooth",
    });
  }, [messages, open]);

  const persist = (next: Record<string, string>) => {
    setTaught(next);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
  };

  const addBot = (text: string) =>
    setMessages((prev) => [...prev, { role: "bot", text }]);

  const addUser = (text: string) =>
    setMessages((prev) => [...prev, { role: "user", text }]);

  const handleSend = async (text?: string) => {
    const trimmed = (text ?? input).trim();
    if (!trimmed || submitting) return;
    setInput("");
    addUser(trimmed);

    const match = trimmed.match(teachRegex);
    if (match) {
      const [, , q, a] = match;
      const next = { ...taught, [q.trim().toLowerCase()]: a.trim() };
      if (Object.keys(next).length > MAX_TAUGHT) {
        const oldestKey = Object.keys(next)[0];
        delete next[oldestKey];
      }
      persist(next);
      addBot(`Got it! I saved: "${q.trim()}"`);
      return;
    }

    switch (state.step) {
      case "idle":
        handleIdle(trimmed);
        break;
      case "type":
        handleType(trimmed);
        break;
      case "name":
        handleName(trimmed);
        break;
      case "email":
        handleEmail(trimmed);
        break;
      case "phone":
        handlePhone(trimmed);
        break;
      case "company":
        handleCompany(trimmed);
        break;
      case "demo":
        handleDemo(trimmed);
        break;
      case "callback":
        await handleCallback(trimmed);
        break;
      default:
        const hint = getApiHint(trimmed);
        if (hint !== "none") {
          const apiAnswer = await askApi(trimmed);
          if (apiAnswer) {
            addBot(apiAnswer);
            return;
          }
        }
        const answer = findAnswer(trimmed, taught);
        if (answer) addBot(answer);
        else addBot("I'm not sure about that. You can teach me by typing `learn: your question = my answer`. Or would you like to request a callback?");
    }
  };

  const handleIdle = async (text: string) => {
    const lower = text.toLowerCase();
    if (lower.includes("agency") || lower.includes("onboard")) {
      setDraft((d) => ({ ...d, userType: "Agency", message: text }));
      setState({ step: "name" });
      addBot("Great! Let's get your agency onboarded. First, what's your full name?");
    } else if (lower.includes("individual") || lower.includes("property") || lower.includes("portfolio")) {
      setDraft((d) => ({ ...d, userType: "Individual", message: text }));
      setState({ step: "name" });
      addBot("Perfect — I'll help you get set up to manage your property portfolio. What's your full name?");
    } else if (lower.includes("demo")) {
      setDraft((d) => ({ ...d, demoRequested: true, message: text }));
      setState({ step: "name" });
      addBot("Awesome! I'd love to set up a demo for you. Let's start with your full name?");
    } else {
      const hint = getApiHint(text);
      if (hint !== "none") {
        const apiAnswer = await askApi(text);
        if (apiAnswer) {
          addBot(apiAnswer);
          return;
        }
      }
      const answer = findAnswer(text, taught);
      if (answer) addBot(answer);
      else addBot("I can help you onboard an agency, request a demo, or manage your property portfolio. Which of these sounds like you?");
    }
  };

  const handleType = (text: string) => {
    const lower = text.toLowerCase();
    if (lower.includes("agency")) {
      setDraft((d) => ({ ...d, userType: "Agency" }));
      setState({ step: "name" });
      addBot("Got it — agency onboarding. What's your full name?");
    } else {
      setDraft((d) => ({ ...d, userType: "Individual" }));
      setState({ step: "name" });
      addBot("No problem — property portfolio management. What's your full name?");
    }
  };

  const handleName = (text: string) => {
    setDraft((d) => ({ ...d, fullName: text }));
    setState({ step: "email" });
    addBot(`Nice to meet you, ${text}. What's your email address?`);
  };

  const handleEmail = (text: string) => {
    if (!text.includes("@") || !text.includes(".")) {
    if (!emailRegex.test(text)) {
      addBot("That doesn't look like a valid email. Could you double-check?");
      return;
    }
    setDraft((d) => ({ ...d, email: text }));
    setState({ step: "phone" });
    addBot("And your phone number?");
  };

  const handlePhone = (text: string) => {
    setDraft((d) => ({ ...d, phone: text }));
    if (draft.userType === "Agency") {
      setState({ step: "company" });
      addBot("What's your agency or company name?");
    } else {
      setState({ step: "demo" });
      addBot("Would you like to schedule a demo? (yes/no)");
    }
  };

  const handleCompany = (text: string) => {
    setDraft((d) => ({ ...d, company: text }));
    setState({ step: "demo" });
    addBot("Would you like to schedule a demo? (yes/no)");
  };

  const handleDemo = (text: string) => {
    const lower = text.toLowerCase();
    const demo = lower === "yes" || lower === "y" || lower.includes("demo");
    setDraft((d) => ({ ...d, demoRequested: demo }));
    setState({ step: "callback" });
    if (demo) addBot("Great, I'll mark you down for a demo. Would you like a callback from our team? (yes/no)");
    else addBot("No problem. Would you like a callback from our team to discuss your needs? (yes/no)");
  };

  const handleCallback = async (text: string) => {
    const lower = text.toLowerCase();
    const callback = lower === "yes" || lower === "y" || lower.includes("callback");
    setDraft((d) => ({ ...d, callbackRequested: callback }));

    if (callback) {
      addBot("Excellent — I'll flag this for a callback. One moment while I save your details...");
    } else {
      addBot("No worries. I'll save your details and our team will reach out shortly. One moment...");
    }

    setSubmitting(true);
    try {
      const payload = {
        fullName: draft.fullName,
        email: draft.email,
        phone: draft.phone,
        company: draft.company || undefined,
        userType: draft.userType,
        package: draft.userType === "Agency" ? "SMEAgency" : "Private",
        demoRequested: draft.demoRequested,
        message: draft.message || undefined,
        source: "bibsy",
      };

      const res = await fetch("/api/leads", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });

      if (!res.ok) throw new Error("Failed to submit");

      const data = await res.json();
      setState({ step: "done" });

      if (callback) {
        addBot(
          `Your details have been saved (ref: ${data.leadId}). A member of our team will call you at ${draft.phone} shortly. Is there anything else I can help with?`
        );
      } else {
        addBot(
          `Your details have been saved (ref: ${data.leadId}). A broker will contact you at ${draft.email} shortly. Is there anything else I can help with?`
        );
      }
    } catch {
      addBot(`Sorry, something went wrong saving your details. Please try again or email us at ${process.env.NEXT_PUBLIC_CONTACT_EMAIL || "esauselo@gmail.com"}.`);
    } finally {
      setSubmitting(false);
    }
  };

  const onKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter") handleSend();
  };

  const askApi = async (query: string): Promise<string | null> => {
    try {
      const res = await fetch("/api/bibsy", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ query }),
      });
      if (!res.ok) return null;
      const data = await res.json();
      return data.answer ?? null;
    } catch {
      return null;
    }
  };

  const quickActions = state.step === "idle"
    ? ["Agency onboarding", "Demo request", "Property portfolio"]
    : [];

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className={`fixed bottom-6 right-6 z-50 flex h-14 w-14 items-center justify-center rounded-full bg-gradient-to-br from-blue-600 to-emerald-500 text-white shadow-lg shadow-blue-600/25 transition-transform hover:scale-105 hover:shadow-xl focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600`}
        aria-label="Open Bibsy chat"
        title="Bibsy — ask me anything"
      >
        <MessageCircle className="h-6 w-6" />
      </button>

      {open && (
        <div
          className="fixed inset-0 z-50 flex items-end justify-end md:items-center"
          role="presentation"
        >
          <div
            className="absolute inset-0 bg-slate-900/50 md:bg-slate-900/40"
            onClick={() => setOpen(false)}
            aria-hidden="true"
          />
          <div
            className="pointer-events-auto fixed bottom-0 right-0 z-[60] m-0 flex h-[32rem] w-80 max-w-[90vw] flex-col rounded-t-2xl bg-white shadow-card ring-1 ring-slate-200 border border-slate-200 md:m-6 md:rounded-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <header className="flex items-center justify-between rounded-t-2xl bg-gradient-to-r from-blue-600 to-emerald-500 px-4 py-3 text-white">
              <div className="flex items-center gap-2">
                <Bot className="h-5 w-5" />
                <span className="font-semibold">Bibsy</span>
                <span className="hidden text-xs opacity-80 sm:inline">
                  — real-estate assistant
                </span>
              </div>
              <button
                type="button"
                onClick={() => setOpen(false)}
                className="rounded-xl p-1 text-white/80 hover:bg-white/20 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
                aria-label="Close chat"
              >
                <X className="h-4 w-4" />
              </button>
            </header>

            <ul
              ref={listRef}
              className="flex-1 space-y-3 overflow-y-auto p-4 text-sm text-slate-700"
            >
              {messages.map((m, i) => (
                <li
                  key={`${m.role}-${i}`}
                  className={
                    m.role === "user"
                      ? "ml-auto max-w-[85%] rounded-2xl rounded-br-md bg-blue-600 px-3.5 py-2 text-white"
                      : "mr-auto max-w-[85%] rounded-2xl rounded-bl-md bg-slate-100 px-3.5 py-2 text-slate-800"
                  }
                >
                  {m.text}
                </li>
              ))}
              {submitting && (
                <li className="mr-auto max-w-[85%] rounded-2xl rounded-bl-md bg-slate-100 px-3.5 py-2 text-slate-500 italic">
                  Saving...
                </li>
              )}
            </ul>

              {quickActions.length > 0 && !submitting && (
                <div className="flex gap-2 px-4 pb-2">
                  {quickActions.map((action) => (
                    <button
                      key={action}
                      type="button"
                      onClick={() => handleSend(action)}
                      className="rounded-lg border border-slate-200 bg-slate-50 px-3 py-1.5 text-xs font-medium text-slate-700 transition-colors hover:bg-blue-50 hover:text-blue-700 hover:border-blue-200"
                    >
                      {action}
                    </button>
                  ))}
                </div>
              )}

            {state.step !== "done" && !submitting && (
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  handleSend();
                }}
                className="flex items-center gap-2 border-t border-slate-200/70 p-3"
              >
                <input
                  value={input}
                  onChange={(e) => setInput(e.target.value)}
                  onKeyDown={onKeyDown}
                  type="text"
                  placeholder={
                    state.step === "type" ? "Agency or Individual?" :
                    state.step === "name" ? "Your full name" :
                    state.step === "email" ? "you@example.com" :
                    state.step === "phone" ? "+27 82 000 0000" :
                    state.step === "company" ? "Agency name" :
                    state.step === "demo" ? "yes or no" :
                    state.step === "callback" ? "yes or no" :
                    "Ask Bibsy..."
                  }
                  className="flex-1 rounded-xl border border-slate-300 bg-slate-50 px-3 py-2 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-blue-600/30"
                />
                <button
                  type="submit"
                  disabled={submitting}
                  className="rounded-xl bg-blue-600 p-2 text-white transition-colors hover:bg-blue-700 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 disabled:opacity-50"
                  aria-label="Send"
                >
                  <Send className="h-4 w-4" />
                </button>
              </form>
            )}
          </div>
        </div>
      )}
    </>
  );
}
