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
      "Applications, consulted land offers and allocations",
      "Clear jurisdiction outlines, automatic parcel zoom, editable maps with re-center controls and grouped review actions",
      "Jurisdiction workspaces, roles and reporting",
      "Jurisdiction-set application fees and unpaid-fee reports",
      "Beneficiary agreements, bank details and external payment records",
      "Encrypted organisation receiving accounts",
      "Google Cloud deployment options for organisations",
      "Area-based and negotiated owner compensation, configurable VAT",
      "Verified applicant accounts, application motivation and supporting documents",
      "Manual fee, land purchase and royalty payments with proof uploads and staff verification",
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
