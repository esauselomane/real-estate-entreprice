import { useEffect, useState } from "react";
import {
  ArrowRight,
  Building2,
  Users,
  Briefcase,
  BarChart3,
  FileText,
  ShieldCheck,
  Check,
} from "lucide-react";
import "./RealEstateLanding.css";
const companyUrl =
  import.meta.env.VITE_COMPANY_SITE_URL || "https://www.biscovetech.com";
const capabilities = [
  {
    title: "Property inventory",
    description:
      "Maintain listings, units, amenities, media and documents. Locate stock on an interactive map.",
    icon: Building2,
  },
  {
    title: "Leads and deals",
    description:
      "Capture enquiries, assign brokers and follow opportunities from first contact through negotiation and close.",
    icon: Briefcase,
  },
  {
    title: "People and CRM",
    description:
      "Keep companies, contacts, owners and tenants connected to your portfolio.",
    icon: Users,
  },
  {
    title: "Dashboards and reports",
    description:
      "See pipeline activity and broker performance, with reports for agency leadership.",
    icon: BarChart3,
  },
  {
    title: "Presentations and reach",
    description:
      "Build branded client pitch packs from your inventory and manage listing distribution.",
    icon: FileText,
  },
  {
    title: "Agency control",
    description:
      "Manage organisation workspaces, roles, permissions, data imports and subscription billing.",
    icon: ShieldCheck,
  },
];
function usePublicPricing() {
  const [plans, setPlans] = useState([]);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    const controller = new AbortController();
    async function loadPricing() {
      try {
        const gateway = (
          import.meta.env.VITE_API_BASE_URL || "http://localhost:5000/gateway"
        ).replace(/\/$/, "");
        const response = await fetch(
          `${gateway}/system/lead-requests/pricing-packages`,
          { signal: controller.signal },
        );
        if (!response.ok) return;
        const data = await response.json();
        if (!controller.signal.aborted && Array.isArray(data)) setPlans(data);
      } catch {
        /* Sales remains available when live pricing cannot be loaded. */
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    }
    void loadPricing();
    return () => controller.abort();
  }, []);
  return { plans, loading };
}
function ProductPricing() {
  const { plans, loading } = usePublicPricing();
  return (
    <section id="pricing" className="re-section">
      <div className="re-section-heading">
        <p className="re-eyebrow">Plans for your agency</p>
        <h2>Choose room to grow.</h2>
        <p>
          Hosted access to inventory, pipeline, people and agency
          administration.
        </p>
      </div>
      {loading ? (
        <p role="status">Loading current plans…</p>
      ) : plans.length === 0 ? (
        <p>
          Current pricing is unavailable.{" "}
          <a href={`${companyUrl}/contact?solution=real-estate`}>
            Contact sales for a quote.
          </a>
        </p>
      ) : (
        <>
          <div className="re-plans">
            {plans.map((plan) => (
              <article
                key={plan.code}
                className={`re-plan ${plan.highlighted ? "re-plan-featured" : ""}`}
              >
                {plan.highlighted && <p className="re-eyebrow">Most popular</p>}
                <h3>{plan.name}</h3>
                <p>{plan.description}</p>
                <p className="re-price">
                  {plan.code === "ENTERPRISE" || plan.monthlyPrice == null
                    ? "Contact sales"
                    : `R${Number(plan.monthlyPrice).toLocaleString("en-ZA")}`}{" "}
                  {plan.code !== "ENTERPRISE" && plan.monthlyPrice != null && (
                    <small>/month</small>
                  )}
                </p>
                {plan.code !== "ENTERPRISE" && plan.annualPrice != null && (
                  <p>
                    or R{Number(plan.annualPrice).toLocaleString("en-ZA")}/year
                  </p>
                )}
                <ul>
                  {(plan.features || []).map((feature) => (
                    <li key={feature}>
                      <Check size={16} />
                      {feature}
                    </li>
                  ))}
                </ul>
                <a
                  className="re-button re-button-secondary"
                  href={
                    plan.code === "ENTERPRISE"
                      ? `${companyUrl}/contact?solution=real-estate&package=ENTERPRISE`
                      : `/?billing=1&package=${encodeURIComponent(plan.code)}#sign-in`
                  }
                >
                  {plan.code === "ENTERPRISE"
                    ? "Talk to sales"
                    : `Start with ${plan.name}`}
                </a>
              </article>
            ))}
          </div>
          <p className="re-small">
            Prices in ZAR, excluding VAT. Plans are loaded from the current
            product catalogue.
          </p>
        </>
      )}
    </section>
  );
}
function ProductFeatures() {
  return <section className="re-section">
    <h1>Workspace features</h1>
    <p>Tools for your property inventory, clients and daily agency work.</p>
    <div className="re-features">{capabilities.map(({ title, description, icon: Icon }) => (
      <article key={title}><Icon size={20} /><h2>{title}</h2><p>{description}</p></article>
    ))}</div>
  </section>;
}

export default function RealEstateLanding({ children }) {
  const page = window.location.pathname;
  const pricing = page === "/pricing" || window.location.hash === "#pricing";
  const features = page === "/features";
  return <div className="real-estate-landing">
    <header className="re-header">
      <a className="re-brand" href="/"><img src="/favicon.jpg" alt="" />Biscove <span>Real Estate</span></a>
      <nav aria-label="Product navigation">
        <a href="/features" aria-current={features ? "page" : undefined}>Features</a>
        <a href="/pricing" aria-current={pricing ? "page" : undefined}>Pricing</a>
        <a href={`${companyUrl}/onboarding`}>Start a trial</a>
        <a href="/#sign-in">Sign in</a>
      </nav>
    </header>
    <main>
      {pricing ? <ProductPricing /> : features ? <ProductFeatures /> : (
        <section className="re-entry">
          <div className="re-entry-copy">
            <p className="re-eyebrow">Biscove Real Estate</p>
            <h1>Your agency workspace</h1>
            <p>Manage properties, clients and deals in one place.</p>
            <ul className="re-summary">
              <li><Building2 size={19} /><span>Property inventory, units and maps</span></li>
              <li><Users size={19} /><span>Client records and broker collaboration</span></li>
              <li><Briefcase size={19} /><span>Leads, deals and reporting</span></li>
            </ul>
            <a className="re-text-link" href="/features">Explore the workspace <ArrowRight size={16} /></a>
          </div>
          <div id="sign-in" className="re-login">{children}</div>
        </section>
      )}
    </main>
    <footer className="re-footer">
      <a href={companyUrl}>Biscove Technologies</a>
      <a href={`${companyUrl}/contact?solution=real-estate`}>Contact support</a>
    </footer>
  </div>;
}
