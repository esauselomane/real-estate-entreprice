import Home from "@/components/sections/Hero";
import ProductFeatures from "@/components/sections/ProductFeatures";
import ValueProp from "@/components/sections/ValueProp";
import CtaStripe from "@/components/sections/CtaStripe";

export const metadata = {
  title: "Biscove Real Estate Technology",
  description:
    "Hosted workspace for property inventory, leads and deals, CRM, reports, brochures, and listing distribution.",
};

export default function HomePage() {
  return (
    <div className="bg-slate-900">
      <Home />
      <ProductFeatures />
      <ValueProp />
      <CtaStripe />
    </div>
  );
}
