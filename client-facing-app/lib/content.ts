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
  tagline: "One platform for full-lifecycle real-estate deals.",
  description:
    "CRM, property listings, automated brochure generation and market syndication - unified for brokers, agencies and enterprise teams. We align the code, you sell the abode.",
  url: process.env.NEXT_PUBLIC_SITE_URL || "https://realestate.example",
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
    description: "",
  },
  {
    image: "/pleasing.png",
    description: "Never let a lead slip through the cracks. Our intelligent CRM transforms unverified leads into a structured, visual sales pipeline."
  },
  {
    image: "/closeindays.png",
    description: "Track every deal from the initial introduction through negotiation, while a built-in leaderboard keeps your brokerage team motivated and aligned."
  },
  {
    image: "/rolebased.png",
    description: "The platform is fully secure, offering tailored access levels for everyone involved.",
  },
  {
    image: "/automatescale.png",
    description: "Modern, scalable foundation built to elevate your real estate enterprise."
  },
  {
    image: "/elevatedemo.png",
    description: "",
    isClickable: true,
    url: "/contact",
  }
];

export const hero: { headline: string; subhead: string; badge?: string; punchline?: string } = {
  headline: "Close more deals with one unified workspace.",
  subhead:
    "A fully hosted SaaS platform - no servers, no IT overhead, no hosting costs. Just pay per month and save thousands in infrastructure and maintenance.",
  badge: "SaaS - zero hosting costs",
  punchline: "We align the code, you sell the abode.",
};

export const features: Feature[] = [
  {
    title: "Deal pipeline CRM",
    description: "Track leads, brokers and deals from first contact to signature.",
    icon: "Briefcase",
  },
  {
    title: "Property listings",
    description: "Catalog portfolios with units, imagery and spatial metrics.",
    icon: "Building2",
  },
  {
    title: "Brochure automation",
    description: "Instantly generate branded Client Pitch Packs that win buyers over.",
    icon: "FileText",
  },
  //{
  //  title: "Image processing",
  //  description: "Auto-resize, watermark and optimise property imagery on the fly.",
  //  icon: "Image",
  //},
  {
    title: "Market syndication",
    description: "Push listings to multiple channels from one source of truth.",
    icon: "Globe",
  },
  {
    title: "Multi-tenant security",
    description: "Isolated workspaces per agency, broker and tenant.",
    icon: "Shield",
  },
  {
    title: "Zero hosting costs",
    description: "Fully hosted SaaS - no servers, no IT overhead, no infrastructure bills.",
    icon: "ServerOff",
  },
];

export const values: Value[] = [
  {
    title: "Broker-first",
    description: "Every workflow is designed around the person who closes the deal.",
    icon: "UserCheck",
  },
  {
    title: "Trust",
    description: "Transparent data, auditable pipelines, secure by default.",
    icon: "HeartHandshake",
  },
  {
    title: "Automation",
    description: "Choreograph background Workers so humans focus on relationships.",
    icon: "Bot",
  },
  {
    title: "Craftsmanship",
    description: "Pixel-perfect brochures and interfaces built to last.",
    icon: "Sparkles",
  },
  {
    title: "Transparency",
    description: "Open deal stages, shared documents, no hidden steps.",
    icon: "Eye",
  },
];

export const impact: Stat[] = [
  { value: "10k+", label: "Brochures generated" },
  { value: "42s", label: "Avg. brochure turnaround" },
  { value: "28", label: "Channels syndicated" },
  { value: "99.9%", label: "Uptime" },
  { value: "R50k+", label: "Saved on hosting per year" },
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
    description: "For growing agencies that need more power and reach.",
    monthlyPrice: 299,
    annualPrice: 2990,
    features: [
      "Up to 5 users",
      "20 property listings",
      "Basic brochure generation",
      "Email support",
      "Standard CRM pipeline",
    ],
  },
  {
    code: "SME_AGENCY",
    name: "SME Agency",
    description: "Ideal for small agencies getting started with digital deal management.",
    monthlyPrice: 799,
    annualPrice: 7990,
    highlighted: true,
    features: [
      "Up to 20 users",
      "Unlimited listings",
      "Advanced client pitch packs",
      "Priority support",
      "Market syndication",
      "Custom branding",
    ],
  },
  {
    code: "ENTERPRISE",
    name: "Enterprise",
    description: "Full-scale platform access for large organizations with custom needs.",
    monthlyPrice: 1999,
    annualPrice: 19990,
    features: [
      "Unlimited users",
      "Unlimited listings",
      "Dedicated account manager",
      "API access",
      "Custom integrations",
      "On-premise option",
      "SLA guarantee",
      "Advanced analytics",
    ],
  },
];

export const testimonials: { quote: string; name: string; role: string; logo: string }[] = [
  /*{
    quote:
      "The brochure Worker turned a 2-hour manual task into a 42-second click. Deals move faster now.",
    name: "Thandi Mokoena",
    role: "Director, Mokoena Properties",
    logo: "M",
  },
  {
    quote:
      "One workspace for CRM, listings and syndication. Our conversion rate improved noticeably.",
    name: "Raj Patel",
    role: "Head of Brokerage, Nexus Group",
    logo: "N",
  },*/
];

export const timeline: { year: string; event: string }[] = [
  { year: "2022", event: "Founded to solve deal-closing fragmentation for brokers." },
  { year: "2023", event: "Launched the CRM pipeline and property catalogue." },
  { year: "2024", event: "Introduced background Workers for brochure + image processing." },
  { year: "2025", event: "Added market syndication and multi-tenant isolation." },
];

export const leadership: TeamMember[] = [
  {
    name: "Amara Okafor",
    role: "Chief Executive Officer",
    image: "/images/team/amara.jpg",
    linkedin: "#",
  },
  {
    name: "Derek Liu",
    role: "Chief Technology Officer",
    image: "/images/team/derek.jpg",
    linkedin: "#",
  },
  {
    name: "Siyabonga Dlamini",
    role: "Head of Product",
    image: "/images/team/siyabonga.jpg",
    linkedin: "#",
  },
];

export type IconMap = {
  [key: string]: string;
};
