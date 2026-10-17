export type BibsyEntry = {
  question: string;
  answer: string;
  keywords: string[];
  apiHint?: "pricing" | "properties" | "none";
};

export const bibsyKnowledge: BibsyEntry[] = [
  { question: "What does Biscove Technologies do?", answer: "Biscove Technologies builds hosted business software. Our solutions are Biscove Real Estate for brokerage operations and Biscove Land Administration for managing parcels, jurisdictions and land workflows. Visit /solutions to explore both.", keywords: ["biscove", "technologies", "company", "solutions"], apiHint: "none" },
  { question: "What is Land Administration?", answer: "Biscove Land Administration supports rural and communal land authorities with parcel registers, GIS imports, country-specific coordinate systems, jurisdiction workspaces, roles and reporting. Features include Guided onboarding setup with our BA, Payfast integration, Multi-currency support and Tenant land-use transfer. It includes visual collection reports, tenant invoices and Finance reconciliation, historical capture using phone GPS or coordinates with independent approval, separate municipal workspaces with explicitly linked jurisdictions and planning restrictions, branded allocation certificates, allocated-parcel maps with disclosure-approved holder names, and authority-led Proof of Allocation templates. Application boundaries can be captured using phone GPS, uploaded coordinates or map drawing. Municipal alerts use a configured notification email, defaulting to the original tenant administrator email where recorded. Features depend on configuration and permissions. Multi-currency display does not imply every payment currency is supported. Institutional signing requires configured keys; these records are not state title deeds. Visit /solutions/land-administration or contact our team.", keywords: ["land", "administration", "gis", "jurisdiction"], apiHint: "none" },
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
      "Visit Our Solutions and open your product. The real estate product has its own Sign in section; /login remains a shortcut to it.",
    keywords: ["login", "log", "in", "auth", "sign", "broker"],
    apiHint: "none",
  },
  {
    question: "How do I contact you?",
    answer:
      "Use the Contact page, or email " + (process.env.NEXT_PUBLIC_CONTACT_EMAIL || "admin@biscovetech.com") + ".",
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
  if (/\b(land|gis|jurisdictions?)\b/i.test(input)) return bibsyKnowledge[1].answer;
  if (/\b(company|technologies|solutions)\b/i.test(input)) return bibsyKnowledge[0].answer;
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
  if (/\b(land|gis|jurisdictions?)\b/i.test(q)) return "none";
  if (["price", "pricing", "plan", "package", "cost", "rate", "subscription"].some((k) => q.includes(k))) return "pricing";
  return "none";
}
