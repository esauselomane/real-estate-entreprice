export type BibsyEntry = {
  question: string;
  answer: string;
  keywords: string[];
  apiHint?: "pricing" | "properties" | "none";
};

export const bibsyKnowledge: BibsyEntry[] = [
  { question: "What does Biscove Technologies do?", answer: "Biscove Technologies builds hosted business software. Our solutions are Biscove Real Estate for brokerage operations and Biscove Land Administration for managing parcels, jurisdictions and land workflows. Visit /solutions to explore both.", keywords: ["biscove", "technologies", "company", "solutions"], apiHint: "none" },
  { question: "What is Land Administration?", answer: "Biscove Land Administration supports rural and communal land under chiefs and kings, with boundaries established in accordance with applicable government requirements. It manages land registers, GIS imports, jurisdiction workspaces, applications and consulted allocation workflows. Authorised staff can preview each requested parcel on the applications page, with automatic zoom to its boundary when the map becomes visible. Selecting View parcel brings the requested area into view even from a scrolled application list. Selected application jurisdictions are shown as thin outlines with transparent parcel overlays. Staff can inspect offer boundaries across review statuses and reshape parcels through revised offers for applicant acceptance. Maps appear first in application review and include a re-center control. Authorised staff can edit beneficiary agreements, update bank details and record multiple beneficiaries on one affected parcel with explicit agreed individual percentages. Application inspection and decision actions are grouped separately, with spacing and touch-friendly controls on smaller screens. Jurisdiction administrators can configure application fees; each submission keeps its quoted fee. Authorised staff can record verified external fee payments and report unpaid applications with jurisdiction/date filters and CSV export. Organisation receiving accounts are encrypted, and remaining land proceeds can be recorded as paid externally only after affected owner payments are recorded. The organisation configures its receiving bank account. Applicants can view payment instructions on their own applications and upload PDF, JPEG or PNG proof for administration fees, approved land purchases and royalty payments. Submissions remain pending until authorised staff reconcile the receiving account and verify or reject them. Rejected proofs can be replaced. Verified fee proofs clear the unpaid-fee status; owner payouts remain separately recorded. These records do not transfer money. Standard owner compensation equals the relinquished area divided by the complete application area: an owner relinquishing 15 square metres from a 100 square metre application receives 15 percent of the net sale price. A negotiated percentage can be recorded with justification and consulted agreement, subject to approval and a total compensation cap of the sale value. The remainder goes to the organisation, and with no overlaps the full net sale value goes to the organisation. VAT defaults to 15 percent, is configurable per jurisdiction, is snapshotted on each offer and is shown separately from compensation and tenant proceeds. Historical accepted agreements retain their recorded terms unless a permitted revision is issued. Development-only fee simulation requires explicit configuration and is disabled in production. Applicants must explain their reasons for applying and can attach supporting documents. ID copies may be attached at submission or added later, but approval is blocked until an ID copy is present. Application evidence is encrypted and accessible only to the applicant and authorised staff within the relevant jurisdiction. Evidence and motivation can be updated during review and are locked after approval. Applicants verify their email through a link valid for 30 minutes, then create a password of at least eight characters with uppercase, lowercase and a special character. Proof of Allocation and Land Use Right Certificates can be digitally signed by the Traditional Council and stamped by the Municipality once institutional signing credentials are configured. Organisation administrators can upload PNG or JPEG logos and authority seals for new certificate PDFs and download clearly marked sample PDFs using fictional allocation details. Existing PDFs retain their artwork and signatures after branding changes. Uploaded seal artwork does not replace institutional approval. The Allocation map page shows allocated parcels within the tenant user’s authorised areas and selected jurisdiction workspace. Clicking a parcel or selecting it from the list shows its reference, jurisdiction, permitted use, area and allocation date. Holder names remain hidden unless an authorised manager records consent or a specific legal disclosure obligation under Allocations. This is separate from registered-parcel disclosure settings. Contact details, identity numbers, banking data and consent evidence references are excluded from the map response. Jurisdictions can have their own certificate logo, seal, location description, paramount authority and presiding Headman. New Proof of Allocation drafts follow an authority-led template with beneficiary details, a parcel drawing, conditions and separate Council and municipal endorsements. Jurisdiction sample downloads use recorded parcel boundaries and fictional holder details without issuing an allocation. Existing signed PDFs retain their original layout and artwork. These documents do not confer title ownership. Visit /solutions/land-administration to explore this product, or contact our team to discuss your organisation.", keywords: ["land", "administration", "gis", "jurisdiction"], apiHint: "none" },
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
