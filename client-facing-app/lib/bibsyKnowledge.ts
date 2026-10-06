export type BibsyEntry = {
  question: string;
  answer: string;
  keywords: string[];
  apiHint?: "pricing" | "properties" | "none";
};

export const bibsyKnowledge: BibsyEntry[] = [
  { question: "What does Biscove Technologies do?", answer: "Biscove Technologies builds hosted business software. Our solutions are Biscove Real Estate for brokerage operations and Biscove Land Administration for managing parcels, jurisdictions and land workflows. Visit /solutions to explore both.", keywords: ["biscove", "technologies", "company", "solutions"], apiHint: "none" },
  { question: "What is Land Administration?", answer: "Biscove Land Administration supports rural and communal land under chiefs and kings, with boundaries established in accordance with applicable government requirements. It manages land registers, GIS imports, jurisdiction workspaces, applications and consulted allocation workflows. Authorised staff can preview each requested parcel on the applications page, with automatic zoom to its boundary. Selected application jurisdictions are shown as thin outlines with transparent parcel overlays. Staff can also inspect offer boundaries across review statuses. Application inspection and decision actions are grouped separately, with spacing and touch-friendly controls on smaller screens. Jurisdiction administrators can configure application fees; each submission keeps its quoted fee. Authorised staff can record verified external fee payments and report unpaid applications with jurisdiction/date filters and CSV export. Organisation receiving accounts are encrypted, and remaining land proceeds can be recorded as paid externally only after affected owner payments are recorded. These records do not transfer money. The current consulted owner agreement divides the full sale price among affected owners, so such agreements leave no tenant balance. Development-only fee simulation requires explicit configuration and is disabled in production. Applicants verify their email through a link valid for 30 minutes, then create a password of at least eight characters with uppercase, lowercase and a special character. Proof of Allocation and Land Use Right Certificates can be digitally signed by the Traditional Council and stamped by the Municipality once institutional signing credentials are configured. These documents do not confer title ownership. Container deployment configuration is available for Google Cloud, with separate organisation services and protected database and signing-key storage; hosting must be configured for each deployment. Visit /solutions/land-administration to explore this product, or contact our team to discuss your organisation.", keywords: ["land", "administration", "gis", "jurisdiction"], apiHint: "none" },
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
