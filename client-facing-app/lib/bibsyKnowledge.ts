export type BibsyEntry = {
  question: string;
  answer: string;
  keywords: string[];
  apiHint?: "pricing" | "properties" | "none";
};

export const bibsyKnowledge: BibsyEntry[] = [
  {
    question: "What is Biscove Real Estate Tech?",
    answer:
      "Biscove Real Estate Tech is a multi-tenant platform for brokers and agencies — CRM for leads and deals, property listings, automated branded brochure generation and market syndication.",
    keywords: ["real", "estate", "enterprise", "platform", "what", "is", "app", "software"],
     apiHint: "none",
  },
  {
    question: "Who is Bibsy?",
    answer:
      "I'm Bibsy, your friendly real-estate assistant chatbot. I answer questions about the platform and can learn new answers from you as you teach me.",
    keywords: ["bibsy", "who", "chatbot", "bot", "assistant", "help"],
    apiHint: "none",
  },
  {
    question: "Who is this platform for?",
    answer:
      "It's built for brokers, brokerages and enterprise real-estate teams, plus admins who manage multi-tenant workspaces.",
    keywords: ["broker", "agency", "teams", "enterprise", "who", "for"],
    apiHint: "none",
  },
  {
    question: "How does brochure automation work?",
    answer:
      "A broker curates a brochure card per deal, dispatches it, and a background Worker renders a branded PDF in seconds — the deal then automatically advances to Proposal / Quote Sent.",
    keywords: ["brochure", "pdf", "automat", "worker", "render", "generate", "card"],
    apiHint: "none",
  },
  {
    question: "What are the deal stages?",
    answer:
      "Leads convert to deals that move through: Discovery, Presentation, Proposal/Quote Sent, Negotiation, Closed Won or Closed Lost.",
    keywords: ["deal", "stage", "pipeline", "leads", "closed", "progress"],
    apiHint: "none",
  },
  {
    question: "What about property listings?",
    answer:
      "Properties are cataloged with units, erf size, valuation/sale/rental pricing, lease terms, geo coordinates and image galleries, and shown on an interactive map.",
    keywords: ["property", "listings", "map", "units", "lease", "rental", "listing"],
    apiHint: "none",
  },
  {
    question: "What is market syndication?",
    answer:
      "Listings are distributed to multiple channels from a single source of truth via the PlatformSyndication worker.",
    keywords: ["syndication", "channel", "publish", "distribute", "market"],
    apiHint: "none",
  },
  {
    question: "Is it secure and multi-tenant?",
    answer:
      "Yes — every request is tenant-isolated via the TenantId claim and role-scoped: PlatformAdmin, AgencyAdmin, Broker, Owner and Tenant.",
    keywords: ["secure", "tenant", "multi", "role", "permission", "privacy"],
    apiHint: "none",
  },
  {
    question: "How do I log in?",
    answer:
      "Click 'Login' in the top right, or visit /login — that opens the application login where you enter your email and password.",
    keywords: ["login", "log", "in", "auth", "sign", "broker"],
    apiHint: "none",
  },
  {
    question: "How do I contact you?",
    answer:
      "Reach us at " + (process.env.NEXT_PUBLIC_CONTACT_EMAIL || "hello@realestate.example") + ", or use the Contact page — we'll get back shortly.",
    keywords: ["contact", "email", "reach", "support", "hello"],
    apiHint: "none",
  },
];

export const tokenize = (s: string): string[] =>
  (s.toLowerCase().match(/[a-z0-9]+/g) || []);

const jaccard = (a: string[], b: string[]) => {
  if (!a.length && !b.length) return 1;
  const set = new Set([...a, ...b]);
  const inter = (a.filter((x) => b.includes(x)).length) / set.size;
  return inter;
};

export function findAnswer(
  input: string,
  taught: Record<string, string> | null
): string | null {
  const tokens = tokenize(input);
  if (!tokens.length) return null;

  // 1) Live-taught answers (per-browser)
  for (const question of Object.keys(taught ?? {})) {
    if (jaccard(tokens, tokenize(question)) > 0.6) return taught![question];
  }

  // 2) Curated knowledge base — match by keyword overlap.
  let best: BibsyEntry | null = null;
  let bestScore = 0;
  for (const entry of bibsyKnowledge) {
    const kw = entry.keywords;
    const overlap = kw.filter((k) => tokens.includes(k)).length;
    const score = kw.length ? overlap / kw.length : 0;
    if (score > bestScore) {
      bestScore = score;
      best = entry;
    }
  }
  return bestScore > 0.4 ? best!.answer : null;
}

export function getApiHint(input: string): "pricing" | "none" {
  const q = input.toLowerCase();
  if (["price", "pricing", "plan", "package", "cost", "rate", "subscription"].some((k) => q.includes(k))) return "pricing";
  return "none";
}
