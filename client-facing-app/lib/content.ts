export type Feature = {
  title: string;
  description: string;
  icon: string;
};

export type Stat = {
  value: string;
  label: string;
};

export type Value = {
  title: string;
  description: string;
  icon: string;
};

export type TeamMember = {
  name: string;
  role: string;
  image: string;
  linkedin: string;
};

export const site = {
  name: process.env.NEXT_PUBLIC_SITE_NAME || "Biscove Real Estate Tech",
  tagline: "The digital backbone for South African brokerages.",
  description:
    "An integrated operating system for real estate professionals. Uniting property inventory, lead pipelines, and agency administration into a singular, high-performance workspace.",
  url: process.env.NEXT_PUBLIC_SITE_URL || "https://www.biscovetech.com",
} as const;

export const navigation = [
  { label: "Home", href: "/" },
  { label: "Pricing", href: "/pricing" },
  { label: "About", href: "/about" },
  { label: "Mission", href: "/mission" },
  { label: "Contact", href: "/contact" },
];

export type HeroSlide = {
  image: string;
  description: string;
  isClickable?: boolean;
  url?: string;
};

export const heroSlides: HeroSlide[] = [
  {
    image: "/aligncode.png",
    description: "One workspace for inventory, pipeline, people, and reporting.",
  },
  {
    image: "/pleasing.jpg",
    description: "Capture leads, assign brokers, and move opportunities through a structured deal pipeline.",
  },
  {
    image: "/closeindays.png",
    description: "Search properties, inspect units and documents, and locate stock on the map.",
  },
  {
    image: "/rolebased.png",
    description: "Role-based access for agency administrators, brokers, owners, and tenants.",
  },
  {
    image: "/automatescale.png",
    description: "Generate pitch packs and distribute listings without leaving the workspace.",
  },
  {
    image: "/elevatedemo.png",
    description: "Talk to us about a trial for your agency.",
    isClickable: true,
    url: "/contact",
  },
];

export const hero: { headline: string; subhead: string; badge?: string; punchline?: string } = {
  headline: "Run the brokerage from a single, hosted workspace.",
  subhead:
    "Biscove is more than software; it is the operating system for the modern brokerage. We’ve built the digital infrastructure that transforms fragmented property operations into a singular, professional environment—powering your inventory, your pipeline, and your growth.",
  badge: "Enterprise Workspace for Agencies",
};

export const features: Feature[] = [
  {
    title: "Property inventory",
    description:
      "Search and maintain listings with units, amenities, media, and a document vault. Locate stock on an interactive map.",
    icon: "Building2",
  },
  {
    title: "Leads and deals",
    description:
      "Intake enquiries, suggest brokers, and progress deals from first contact through negotiation to close.",
    icon: "Briefcase",
  },
  {
    title: "People and CRM",
    description:
      "A directory of companies and contacts, plus dedicated records for owners and tenants tied to your portfolio.",
    icon: "Users",
  },
  {
    title: "Dashboards and reports",
    description:
      "Agency and broker views for pipeline activity, plus performance reports and analytics for leadership.",
    icon: "BarChart3",
  },
  {
    title: "Presentations and reach",
    description:
      "Build branded client pitch packs from selected stock, and syndicate listings to external portals from one record.",
    icon: "FileText",
  },
  {
    title: "Agency control",
    description:
      "Multi-tenant workspaces, roles and permissions, agency overview, data import, and subscription billing.",
    icon: "Shield",
  },
];

export const values: Value[] = [
  {
    title: "Clarity",
    description: "Stock, people, and pipeline live in one system of record — not scattered spreadsheets and inboxes.",
    icon: "Eye",
  },
  {
    title: "Accountability",
    description: "Roles, permissions, and deal stages make ownership visible across the brokerage.",
    icon: "UserCheck",
  },
  {
    title: "Presentation quality",
    description: "Pitch packs and listing distribution are produced from the same inventory your team already maintains.",
    icon: "Sparkles",
  },
  {
    title: "Operational discipline",
    description: "Imports, notifications, and administration sit alongside daily brokerage work, not in a separate tool.",
    icon: "Shield",
  },
];

export const impact: Stat[] = [
  { value: "Inventory", label: "Listings, units, maps, documents" },
  { value: "Pipeline", label: "Leads, matching, deals" },
  { value: "People", label: "CRM, owners, tenants" },
  { value: "Control", label: "Roles, reports, billing" },
];

export const pricing: {
  code: string;
  name: string;
  description: string;
  monthlyPrice: number;
  annualPrice: number;
  features: string[];
  highlighted?: boolean;
}[] = [
  {
    code: "PRIVATE",
    name: "Private",
    description: "For independent brokers who need a complete workspace.",
    monthlyPrice: 299,
    annualPrice: 2990,
    features: [
      "Up to 5 users",
      "20 property listings",
      "Brochure generation",
      "Email support",
      "Lead and deal pipeline",
    ],
  },
  {
    code: "SME_AGENCY",
    name: "SME Agency",
    description: "For growing agencies that need capacity and branding.",
    monthlyPrice: 799,
    annualPrice: 7990,
    highlighted: true,
    features: [
      "Up to 20 users",
      "Unlimited listings",
      "Custom client pitch packs",
      "Priority support",
      "Market syndication",
      "Custom branding",
      "Property and contact import",
    ],
  },
  {
    code: "ENTERPRISE",
    name: "Enterprise",
    description: "For groups that need scale, onboarding, and API access.",
    monthlyPrice: 60000,
    annualPrice: 500000,
    features: [
      "Unlimited users and listings",
      "Custom client pitch packs",
      "Market syndication",
      "Dedicated onboarding",
      "Dedicated account manager",
      "Developer API access",
      "Custom integrations",
      "SLA and advanced analytics",
      "Broker-to-lead matching",
    ],
  },
];

export const timeline: { year: string; event: string }[] = [
  { year: "2015", event: "Biscove is established to address fragmented brokerage operations." },
  { year: "2024", event: "Platform architecture for inventory, CRM, and administration is designed." },
  { year: "2026", event: "The hosted workspace opens to agencies." },
];

export const story = {
  title: "The Infrastructure for Excellence",
  narrative:
    "Biscove didn't start in a boardroom; it started in the field. We watched the friction that kept South African agencies from scaling—the leads lost in overflowing inboxes, the inventory scattered across spreadsheets, and the manual struggle of brochure creation. In 2015, we set out to build the backbone for the next generation of agencies.\n\nWe’ve engineered a hosted workspace that serves as the single source of truth for the entire brokerage. By uniting inventory, deal pipelines, and administration into one professional, tenant-isolated environment, we give administrators control and brokers the freedom to focus on human relationships. Today, Biscove is the silent engine driving South Africa's most professional real estate operations.",
};

export const leadership: TeamMember[] = [];

export type IconMap = {
  [key: string]: string;
};
