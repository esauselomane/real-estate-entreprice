export const realEstateUrl =
  process.env.NEXT_PUBLIC_APP_URL || "https://app.biscovetech.com";
export const landAdministrationUrl =
  process.env.NEXT_PUBLIC_LAND_APP_URL ||
  "/contact?solution=land-administration";
export const solutions = [
  {
    id: "real-estate",
    name: "Biscove Real Estate",
    audience: "For brokerages and property teams",
    description:
      "Bring property inventory, client relationships and the deal pipeline into one hosted workspace.",
    features: [
      "Property listings, units, maps and documents",
      "CRM, leads, broker assignments and deals",
      "Reports, branded brochures and listing distribution",
      "Agency workspaces, permissions and billing",
      "Optional public property assistant and confirmed CRM enquiries (API configuration required)",
    ],
    href: realEstateUrl,
    action: "Explore real estate",
    icon: "Building2",
  },
  {
    id: "land-administration",
    name: "Biscove Land Administration",
    audience: "For rural and communal land authorities",
    description:
      "Support rural and communal land administration under chiefs and kings, working with recognised land authorities and communities. Jurisdiction boundaries are established in accordance with applicable government requirements.",
    features: [
      "Parcel registers and GIS dataset imports",
      "Country-specific coordinate system configuration",
      "Guided onboarding setup with our BA",
      "Payfast integration",
      "Visual collection reports, tenant invoices and Finance reconciliation of externally paid settlements",
      "Jurisdiction workspaces, roles and reporting",
      "Phone GPS, coordinate uploads and map drawing for applications and historical capture",
      "English, Sepedi, isiZulu, French and Portuguese navigation, sign-in and common labels, with English fallback",
      "Multi-currency support",
      "Tenant land-use transfer",
      "Separate municipal workspaces, linked jurisdictions, planning restrictions and email alerts",
      "Jurisdiction-set application fees and unpaid-fee reports",
      "Branded allocation certificates with authority logos, seals and configurable institutional signing",
      "Allocated-parcel maps with jurisdiction filters and disclosure-approved holder names",
      "Authority-led Proof of Allocation templates with jurisdiction logos, seals and recorded parcel drawings",
    ],
    href: landAdministrationUrl,
    action: process.env.NEXT_PUBLIC_LAND_APP_URL
      ? "Explore land administration"
      : "Discuss land administration",
    icon: "Map",
  },
];
