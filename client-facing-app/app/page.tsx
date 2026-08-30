import Home from "@/components/sections/Hero";
import ProductFeatures from "@/components/sections/ProductFeatures";
import ValueProp from "@/components/sections/ValueProp";
import Testimonials from "@/components/sections/Testimonials";
import CtaStripe from "@/components/sections/CtaStripe";

export const metadata = {
  title: "Biscove Real Estate Technology",
  description:
    "Close more deals with one unified workspace: CRM, listings, branded brochures and market syndication.",
};

export default function HomePage() {
  return (
    <>
      <Home />
      <ProductFeatures />
      <ValueProp />
      <Testimonials />
      <CtaStripe />
    </>
  );
}
