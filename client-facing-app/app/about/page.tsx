import Link from "next/link";
export const metadata = {
  title: "About Biscove",
  description:
    "Biscove Technologies builds hosted software for property operations and land administration.",
};
export default function AboutPage() {
  return (
    <section className="container py-12 sm:py-16">
      <div className="max-w-3xl">
        <p className="text-sm uppercase tracking-widest text-blue-400">
          About Biscove Technologies
        </p>
        <h1 className="mt-4 text-4xl font-display font-bold sm:text-5xl">
          Better systems for everyday operations.
        </h1>
        <p className="mt-6 text-lg text-slate-300">
          Biscove Technologies develops hosted business software for
          organisations managing property and land. Our products bring records,
          workflows and reporting into shared workspaces with clear access
          controls.
        </p>
        <p className="mt-5 text-lg text-slate-300">
          Biscove Real Estate supports brokerage and property teams. Biscove
          Land Administration supports rural and communal land administered under
          chiefs and kings, in collaboration with recognised land authorities and
          communities. Jurisdiction boundaries are established in accordance with
          applicable government requirements. The platform supports parcel
          records, land applications and consulted allocation workflows. Each product serves
          its own users and operational needs.
        </p>
        <h2 className="mt-10 text-2xl font-display font-bold">How we build</h2>
        <p className="mt-4 text-slate-300">
          We organise software around the people using it: administrators,
          operational teams and the customers or applicants they serve. Shared
          records, role-based access, maps and reporting help each person
          understand their work and responsibilities.
        </p>
        <Link
          href="/solutions"
          className="mt-8 inline-flex rounded-xl bg-blue-600 px-6 py-3 font-semibold"
        >
          Discover our solutions
        </Link>
      </div>
    </section>
  );
}
