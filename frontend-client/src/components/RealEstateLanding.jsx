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
export default function RealEstateLanding({ children }) {
  return (
    <div className="real-estate-landing">
      <header className="re-header">
        <a className="re-brand" href="#">
          <img src="/favicon.jpg" alt="" />
          Biscove <span>Real Estate</span>
        </a>
        <nav aria-label="Product navigation">
          <a href="#features">Features</a>
          <a href="#pricing">Pricing</a>
          <a href={`${companyUrl}/onboarding`}>Start a trial</a>
          <a className="re-button re-button-secondary" href="#sign-in">
            Sign in
          </a>
        </nav>
      </header>
      <main>
        <section className="re-hero">
          <div>
            <p className="re-eyebrow">A solution by Biscove Technologies</p>
            <h1>
              Your properties.
              <br />
              Your people.
              <br />
              <em>One workspace.</em>
            </h1>
            <p className="re-intro">
              Run your brokerage from a single, hosted workspace. Bring property
              inventory, client relationships, deal pipelines and agency
              administration together.
            </p>
            <div className="re-actions">
              <a className="re-button" href={`${companyUrl}/onboarding`}>
                Start a trial
                <ArrowRight size={18} />
              </a>
              <a className="re-button re-button-secondary" href="#sign-in">
                Sign in to your workspace
              </a>
            </div>
            <p className="re-small">
              Built for brokerages and property teams. Hosted and ready for your
              organisation.
            </p>
          </div>
          <aside
            className="re-workspace-preview"
            aria-label="Workspace capabilities"
          >
            <p className="re-eyebrow">From enquiry to close</p>
            <div className="re-preview-title">
              <Building2 size={30} />
              <h2>A clear view of your agency</h2>
            </div>
            <div className="re-preview-grid">
              <div>
                <strong>Inventory</strong>
                <span>Listings · units · maps</span>
              </div>
              <div>
                <strong>Pipeline</strong>
                <span>Enquiries · leads · deals</span>
              </div>
              <div>
                <strong>People</strong>
                <span>Contacts · owners · tenants</span>
              </div>
              <div>
                <strong>Control</strong>
                <span>Roles · reports · billing</span>
              </div>
            </div>
            <p className="re-preview-note">
              <ShieldCheck size={20} />
              Access shaped around your team’s responsibilities.
            </p>
          </aside>
        </section>
        <section id="features" className="re-section">
          <div className="re-section-heading">
            <p className="re-eyebrow">The brokerage workspace</p>
            <h2>Keep the work connected.</h2>
            <p>
              Maintain one inventory and use it across your team’s daily work.
            </p>
          </div>
          <div className="re-features">
            {capabilities.map(({ title, description, icon: Icon }) => (
              <article key={title}>
                <Icon size={28} />
                <h3>{title}</h3>
                <p>{description}</p>
              </article>
            ))}
          </div>
        </section>
        <section className="re-value">
          <div>
            <p className="re-eyebrow">Built around your organisation</p>
            <h2>Clear records. Visible responsibilities.</h2>
          </div>
          <p>
            Organisation workspaces and role-based permissions help
            administrators manage access while brokers focus on their stock,
            clients and opportunities. Reports and pitch packs draw on the
            records your team maintains.
          </p>
        </section>
        <ProductPricing />
        <section id="sign-in" className="re-sign-in">
          <div>
            <p className="re-eyebrow">Your agency workspace</p>
            <h2>Welcome back.</h2>
            <p>Sign in to continue working with your team.</p>
            <p>
              New to Biscove Real Estate?{" "}
              <a href={`${companyUrl}/onboarding`}>Start an agency trial.</a>
            </p>
          </div>
          <div className="re-login">{children}</div>
        </section>
      </main>
      <footer className="re-footer">
        <a href={companyUrl}>Biscove Technologies</a>
        <span>Real estate is one of our solutions.</span>
        <a href={`${companyUrl}/solutions`}>Discover our solutions</a>
        <a href={`${companyUrl}/contact?solution=real-estate`}>Contact us</a>
      </footer>
    </div>
  );
}
