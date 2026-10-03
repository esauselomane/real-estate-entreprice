import Solutions from "@/components/sections/Solutions";
export const metadata = {
  title: "Our Solutions",
  description:
    "Explore Biscove Real Estate and Biscove Land Administration: software for property operations and rural and communal land administration.",
};
export default function SolutionsPage() {
  return (
    <>
      <div className="container pt-12">
        <p className="text-sm uppercase tracking-widest text-blue-400">
          Biscove Technologies
        </p>
        <h1 className="mt-3 text-4xl font-display font-bold sm:text-5xl">
          Software for property and land.
        </h1>
        <p className="mt-5 max-w-2xl text-lg text-slate-300">
          Connected records, clear responsibilities and practical workflows for
          the people managing these assets.
        </p>
      </div>
      <Solutions />
      <section className="container py-12">
        <h2 className="text-2xl font-semibold">A verified path from application to allocation</h2>
        <p className="mt-4 text-slate-300">To apply for land, create an account and verify your email. Then select a participating land authority and a jurisdiction that is accepting applications. The responsible authority reviews each request and decides whether to approve an allocation.</p>
        <p className="mt-4 text-slate-300">Approved allocations can receive a digital Proof of Allocation or Land Use Right Certificate, signed by the Traditional Council and stamped by the Municipality using separately configured institutional signing credentials. The certificate includes allocation details, conditions, a boundary sketch and an online verification link. Authorised recipients receive the PDF with its cryptographic signatures. Public verification shows issuance or revocation without revealing the holder’s personal details. These certificates are allocation records, not title deeds.</p>
      </section>
    </>
  );
}
