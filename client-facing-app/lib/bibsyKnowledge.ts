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
      "Biscove is a hosted workspace for brokerages. It covers property inventory and maps, leads and deals, CRM and client records, dashboards and reports, branded pitch packs, listing syndication, and agency administration.",
    keywords: ["real", "estate", "enterprise", "platform", "what", "is", "app", "software", "workspace"],
     apiHint: "none",
  },
  {
    question: "Who is Bibsy?",
    answer:
      "I'm Bibsy, the site assistant. I answer questions about the Biscove workspace and can remember answers you teach me in this browser.",
    keywords: ["bibsy", "who", "chatbot", "bot", "assistant", "help"],
    apiHint: "none",
  },
  {
    question: "Who is this platform for?",
    answer:
      "It is built for agencies, brokers, and administrators who manage multi-tenant workspaces, including owners and tenants where access is granted.",
    keywords: ["broker", "agency", "teams", "enterprise", "who", "for"],
    apiHint: "none",
  },
  {
    question: "What can I do with properties?",
    answer:
      "Search and maintain listings with units, amenities, media, and documents. There is also a map view for locating stock geographically.",
    keywords: ["property", "listings", "map", "units", "search", "document", "inventory"],
    apiHint: "none",
  },
  {
    question: "How do leads and deals work?",
    answer:
      "Enquiries enter the lead workflow, brokers can be suggested for assignment, and opportunities move through a deal pipeline from discovery to close.",
    keywords: ["deal", "stage", "pipeline", "leads", "closed", "progress", "matching"],
    apiHint: "none",
  },
  {
    question: "What is in the CRM?",
    answer:
      "A directory of companies and contacts, plus dedicated owner and tenant records linked to your portfolio.",
    keywords: ["crm", "contact", "company", "owner", "tenant", "people", "directory"],
    apiHint: "none",
  },
  {
    question: "How does brochure automation work?",
    answer:
      "Brokers select stock into a brochure or pitch pack. The workspace generates a branded PDF from that selection so presentations stay aligned with live inventory.",
    keywords: ["brochure", "pdf", "automat", "pitch", "pack", "generate", "card"],
    apiHint: "none",
  },
  {
    question: "What is market syndication?",
    answer:
      "Approved listings can be distributed to external property portals from the same inventory record, instead of re-entering details per channel.",
    keywords: ["syndication", "channel", "publish", "distribute", "market", "portal"],
    apiHint: "none",
  },
  {
    question: "Is it secure and multi-tenant?",
    answer:
      "Yes. Workspaces are tenant-isolated, with roles such as PlatformAdmin, AgencyAdmin, Broker, Owner, and Tenant, plus a dedicated roles and permissions screen.",
    keywords: ["secure", "tenant", "multi", "role", "permission", "privacy"],
    apiHint: "none",
  },
  {
    question: "How do I log in?",
    answer:
      "Click Login in the header, or visit /login. That opens the application at app.biscovetech.com where you enter your email and password.",
    keywords: ["login", "log", "in", "auth", "sign", "broker"],
    apiHint: "none",
  },
  {
    question: "How do I contact you?",
    answer:
      "Use the Contact page, or email " + (process.env.NEXT_PUBLIC_CONTACT_EMAIL || "esauselo@gmail.com") + ".",
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

  for (const question of Object.keys(taught ?? {})) {
    if (jaccard(tokens, tokenize(question)) > 0.6) return taught![question];
  }

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
