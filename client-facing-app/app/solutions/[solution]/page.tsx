import Link from "next/link";
import { notFound } from "next/navigation";
import { solutions } from "@/lib/solutions";
export function generateStaticParams() { return solutions.map(solution => ({ solution: solution.id })); }
export function generateMetadata({ params }: { params: { solution: string } }) {
  const solution = solutions.find(item => item.id === params.solution);
  return { title: solution?.name, description: solution?.description };
}
export default function SolutionPage({ params }: { params: { solution: string } }) {
  const solution = solutions.find(item => item.id === params.solution);
  if (!solution) notFound();
  return <div className="container py-6">
    <Link href="/solutions" className="text-sm text-blue-300">← Our Solutions</Link>
    <h1 className="mt-4 text-3xl font-bold">{solution.name}</h1>
    <p className="mt-2 text-sm text-blue-300">{solution.audience}</p>
    <p className="mt-3 max-w-3xl text-slate-300">{solution.description}</p>
    <ul className="mt-5 grid gap-2 sm:grid-cols-2">{solution.features.map(feature => <li key={feature} className="rounded-lg border border-slate-700 p-3 text-sm">{feature}</li>)}</ul>
    <Link href={solution.href} className="mt-5 inline-flex rounded-lg bg-blue-600 px-4 py-2 font-semibold">{solution.action}</Link>
    {solution.id === "land-administration" && <AllocationDetails />}
  </div>;
}
function AllocationDetails() {
  return (
      <section className="pt-6 pb-2">
        <h2 className="text-2xl font-semibold">A verified path from application to allocation</h2>
        <p className="mt-4 text-slate-300">To apply for land, create an account and verify your email. Then select a participating land authority and a jurisdiction that is accepting applications. The responsible authority reviews each request and decides whether to approve an allocation.</p>
        <p className="mt-4 text-slate-300">Approved allocations can receive a digital Proof of Allocation or Land Use Right Certificate, signed by the Traditional Council and stamped by the Municipality using separately configured institutional signing credentials. The certificate includes allocation details, conditions, a boundary sketch and an online verification link. Authorised recipients receive the PDF with its cryptographic signatures. Public verification shows issuance or revocation without revealing the holder’s personal details. These certificates are allocation records, not title deeds.</p>
      </section>
  );
}
