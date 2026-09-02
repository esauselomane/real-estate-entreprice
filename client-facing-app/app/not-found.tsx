import Link from "next/link";

export default function NotFound() {
  return (
    <section className="flex min-h-[60vh] items-center justify-center py-20">
      <div className="container flex flex-col items-center gap-6 text-center">
        <div className="text-7xl font-display font-extrabold text-blue-400">404</div>
        <h1 className="text-2xl font-display font-extrabold text-white">
          Page not found
        </h1>
        <p className="max-w-md text-slate-300">
          The page you are looking for doesn&apos;t exist or has moved.
        </p>
        <Link href="/">
          <button
            type="button"
            className="rounded-xl bg-blue-600 px-6 py-2.5 text-base font-semibold text-white transition-all duration-200 ease-in-out hover:scale-105 hover:bg-blue-500 hover:shadow-lg active:scale-95 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-500"
          >
            Back to home
          </button>
        </Link>
      </div>
    </section>
  );
}
