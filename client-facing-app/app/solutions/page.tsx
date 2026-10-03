import Solutions from "@/components/sections/Solutions";
export const metadata = {
  title: "Our Solutions",
  description:
    "Explore Biscove Real Estate and Biscove Land Administration: software for property operations and rural and communal land administration.",
};
export default function SolutionsPage() {
  return (
    <>
      <div className="container pt-6">
        <p className="text-sm uppercase tracking-widest text-blue-400">
          Biscove Technologies
        </p>
        <h1 className="mt-3 text-4xl font-display font-bold sm:text-5xl">
          Software for property and land.
        </h1>
        <p className="mt-3 max-w-2xl text-lg text-slate-300">
          Connected records, clear responsibilities and practical workflows for
          the people managing these assets.
        </p>
      </div>
      <Solutions />
    </>
  );
}
