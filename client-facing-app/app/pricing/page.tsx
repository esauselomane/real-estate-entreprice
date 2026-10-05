import { redirect } from "next/navigation";
import { realEstateUrl } from "@/lib/solutions";
export default function PricingPage() {
  redirect(`${realEstateUrl}/#pricing`);
}
