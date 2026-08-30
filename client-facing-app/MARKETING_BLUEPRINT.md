# Marketing Website Blueprint — Real Estate Enterprise

## 1. Product Context

**Real Estate Enterprise** is a multi-tenant B2B SaaS platform built for real-estate brokers, agencies and enterprise teams.

### Core capabilities
- **Deal pipeline CRM** — leads, brokers and deals from first contact to signature.
- **Property listings** — catalog portfolios with units, imagery and spatial metrics.
- **Brochure automation** — generate branded PDF brochures via background Workers.
- **Image processing** — auto-resize, watermark and optimise property imagery.
- **Market syndication** — push listings to multiple channels from one source of truth.
- **Multi-tenant security** — isolated workspaces per agency, broker and tenant.
- **Lead capture** — public intake from Bibsy chatbot and pricing page.

### Backend architecture
- Identity.Microservice (auth, users, roles, lead capture)
- CRM.Microservice (deal pipeline, contacts)
- Property.Microservice (listings, units, imagery)
- ImageProcessor.Worker + BrochureGeneration.Worker + PlatformSyndication.Worker
- API Gateway (Ocelot) → Kafka event bus → SQL Server

---

## 2. Tech Stack Recommendation

**Continue with the existing stack.** The `client-facing-app` is already a production-ready Next.js marketing site.

| Layer | Choice | Rationale |
|-------|--------|-----------|
| Framework | **Next.js 14** (App Router) | Already in use; SSR/SSG for SEO; file-based routing |
| UI library | **React 18** | Matches Next.js 14 default |
| Styling | **Tailwind CSS 3.4** | Mandated by `.kilo/instructions.md`; no inline styles |
| Icons | **lucide-react** | Already in use; tree-shakeable, consistent weight |
| Typography | **@tailwindcss/typography** | Already installed; for rich-text mission/about sections |
| Animations | **Framer Motion** (add) | Smooth page transitions and scroll reveals |
| Forms | **React Hook Form + Zod** (add) | Type-safe lead capture and contact forms |
| Analytics | **Vercel Analytics** (add) | Zero-config for Next.js |

> Do **not** migrate to `frontend-client` (Vite + React 19 + Bootstrap). That is the broker-facing admin app. The marketing site stays in `client-facing-app`.

---

## 3. Design System

### 3.1 Color palette
```
Primary:     blue-600  (#2563EB)   — CTAs, links, active states
Primary dark: blue-700 (#1D4ED8)  — hover
Accent:      emerald-500 (#10B981) — success, secondary CTAs
Accent dark: emerald-400 (#34D399) — hover
Surface:     white, slate-50
Text:        slate-900 (headings), slate-600 (body), slate-500 (muted)
Border:      slate-200/70
```

### 3.2 Typography scale
```
Display:    text-5xl / sm:text-6xl / font-extrabold / tracking-tight  (hero)
Heading 1:  text-4xl / sm:text-5xl / font-extrabold / tracking-tight  (page titles)
Heading 2:  text-3xl / sm:text-4xl / font-extrabold / tracking-tight  (section titles)
Heading 3:  text-2xl / font-bold                                    (cards)
Body:       text-lg (lead), text-base (default), text-sm (captions)
```

### 3.3 Spacing & layout
- **Container**: `container` (max-w-7xl, centered)
- **Section padding**: `py-16 sm:py-20`
- **Card radius**: `rounded-2xl`
- **Button radius**: `rounded-xl`
- **Shadows**: `shadow-card` (subtle, consistent)

### 3.4 Component primitives
| Primitive | Usage |
|-----------|-------|
| `Section` | `<section className="py-16 sm:py-20"><div className="container">...</div></section>` |
| `Card` | `rounded-2xl border border-slate-200/70 bg-white p-6 shadow-card` |
| `Badge` | `inline-flex rounded-full bg-blue-600 px-2.5 py-0.5 text-xs font-extrabold text-white` |
| `IconBox` | `flex h-10 w-10 items-center justify-center rounded-lg bg-blue-50 text-blue-600` |
| `Stat` | `text-3xl font-extrabold text-blue-600` + `text-sm text-slate-500` |

---

## 4. Page Architecture

```
/
├── page.tsx                    # Landing page
├── about/
│   └── page.tsx                # About Us
├── mission/
│   └── page.tsx                # Mission & Values
├── contact/
│   └── page.tsx                # Contact / Lead capture
└── login/
    └── page.tsx                # Broker login (existing)
```

### 4.1 Landing Page (`/`)
**Sections (top → bottom):**

| # | Section | Component | Purpose |
|---|---------|-----------|---------|
| 1 | Hero | `Hero` | Headline, subhead, dual CTAs (login + video) |
| 2 | Trust bar | `TrustBar` (new) | Logos or stats strip ("10k+ brochures", etc.) |
| 3 | Features | `ProductFeatures` | 6-card grid of platform capabilities |
| 4 | Value prop | `ValueProp` | 3-step workflow with CTA |
| 5 | Testimonials | `Testimonials` | Social proof carousel |
| 6 | CTA stripe | `CtaStripe` | Final conversion banner |

**Enhancements:**
- Add scroll-triggered fade-in animations (Framer Motion).
- Add a sticky secondary nav for long-form scrolling.
- Replace placeholder video thumbnail with a real product screenshot or abstract 3D illustration.

### 4.2 About Us (`/about`)
**Sections:**

| # | Section | Purpose |
|---|---------|---------|
| 1 | Page header | Centered title + one-paragraph summary |
| 2 | Story timeline | Vertical timeline with year markers |
| 3 | Platform stack | Visual badges for each microservice |
| 4 | Leadership | 3-column team grid with avatars |
| 5 | Stats | Impact numbers (brochures, turnaround, etc.) |
| 6 | CTA | "Join us" or "See the platform" button |

**Enhancements:**
- Add real team headshots (replace placeholder circles).
- Add a "Careers" subsection or link.
- Animate timeline items on scroll.

### 4.3 Mission & Values (`/mission`)
**Sections:**

| # | Section | Purpose |
|---|---------|---------|
| 1 | Page header | Centered title |
| 2 | Mission statement | Featured quote block |
| 3 | Vision | Gradient-backed statement card |
| 4 | Values grid | 5-card grid with icons |
| 5 | Impact stats | 4-column metric strip |

**Enhancements:**
- Add a subtle background pattern or gradient mesh.
- Consider a "Manifesto" paragraph beneath the mission quote for depth.

### 4.4 Contact (`/contact`)
**Proposed sections:**

| # | Section | Purpose |
|---|---------|---------|
| 1 | Page header | "Get in touch" |
| 2 | Lead form | Name, email, phone, company, user type, package, message |
| 3 | Info cards | Email, phone, Cape Town office |
| 4 | Map placeholder | Static image or Leaflet embed |

**Form behavior:**
- Client-side validation (Zod schema).
- On submit → POST to `http://localhost:5001/api/system/lead-requests` (or gateway).
- Show success toast; optionally redirect to `/thank-you`.

---

## 5. Component Map

```
components/
├── layout/
│   ├── Header.tsx          # Sticky nav, mobile drawer
│   ├── Footer.tsx          # Columns: product, company, legal
│   └── SectionWrapper.tsx  # Reusable <section className="py-16 sm:py-20"><div className="container">
├── sections/
│   ├── Hero.tsx            # Landing hero
│   ├── ProductFeatures.tsx # 6-card feature grid
│   ├── ValueProp.tsx       # 3-step workflow
│   ├── Testimonials.tsx    # Social proof
│   ├── CtaStripe.tsx       # Final CTA banner
│   ├── TrustBar.tsx        # NEW: logos/stats strip
│   └── LeadForm.tsx        # NEW: contact/lead capture
├── ui/
│   ├── IconByName.tsx      # Lucide icon resolver
│   ├── VideoModal.tsx      # Existing modal
│   ├── Button.tsx          # NEW: consistent CTA primitive
│   ├── Badge.tsx           # NEW: pill badge
│   └── StatCard.tsx        # NEW: metric display
└── chat/
    └── BibsyWidget.tsx     # Existing chatbot
```

---

## 6. Content Strategy

### Copy voice
- **Tone**: Confident, warm, precise. Avoid hype words ("revolutionary", "seamless").
- **Perspective**: Second-person ("your pipeline", "your deals").
- **Localization**: en-ZA spelling; Cape Town address; South African case-study names.

### SEO
- Every page exports `metadata` with `title`, `description`, `openGraph`.
- Use semantic HTML (`<section>`, `<article>`, `<blockquote>`).
- Add `next-sitemap` and `robots.txt` in production.

### Performance
- All images in `/public/images/` with explicit width/height or `fill`.
- Use `next/image` with `sizes` for responsive breakpoints.
- Lazy-load below-the-fold sections with `loading="lazy"` (native) or `next/dynamic`.

---

## 7. Implementation Phases

### Phase 1 — Foundation (current sprint)
- [x] Confirm `.kilo` rules (Tailwind only, no inline styles).
- [x] Verify existing page routes.
- [ ] Standardize `SectionWrapper` component.
- [ ] Add `Button`, `Badge`, `StatCard` primitives.
- [ ] Install Framer Motion + React Hook Form + Zod.

### Phase 2 — Enhance existing pages
- [ ] **Landing**: add `TrustBar`, scroll animations, sticky nav.
- [ ] **About**: replace placeholder avatars, animate timeline.
- [ ] **Mission**: add background mesh, refine typography.
- [ ] **Contact**: build `LeadForm` with validation and API integration.

### Phase 3 — Polish & production
- [ ] Add real imagery (team photos, office, product screenshots).
- [ ] Configure `next-sitemap` and `robots.txt`.
- [ ] Add Vercel Analytics / Plausible.
- [ ] Lighthouse audit (target: 90+ performance, 100 accessibility).

---

## 8. Aesthetic Direction

### Visual language
- **Clean, airy, trustworthy**: generous whitespace, slate neutrals, restrained blue/emerald accents.
- **Editorial feel**: large display type, tight tracking on headings, relaxed line-height on body.
- **Subtle depth**: soft shadows (`shadow-card`), faint gradient orbs in hero, ring-1 borders on cards.
- **Motion**: 60fps, purposeful only — fade-up on scroll, hover lift on cards, smooth page transitions.

### Imagery
- Abstract 3D renders or clean architectural line art for hero (no stock photos of handshakes).
- Team portraits: consistent lighting, neutral backgrounds.
- Product screenshots: browser mockup with subtle drop shadow.

### Accessibility
- Minimum contrast ratio 4.5:1 for body text (slate-600 on white passes).
- Focus-visible rings on all interactive elements (`focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2`).
- Alt text on every image; `aria-label` on icon-only buttons.

---

## 9. File Tree (Target State)

```
client-facing-app/
├── app/
│   ├── about/
│   │   └── page.tsx
│   ├── contact/
│   │   └── page.tsx
│   ├── login/
│   │   └── page.tsx
│   ├── mission/
│   │   └── page.tsx
│   ├── globals.css
│   ├── layout.tsx
│   └── page.tsx
├── components/
│   ├── chat/
│   │   └── BibsyWidget.tsx
│   ├── layout/
│   │   ├── Footer.tsx
│   │   ├── Header.tsx
│   │   └── SectionWrapper.tsx
│   ├── sections/
│   │   ├── CtaStripe.tsx
│   │   ├── Hero.tsx
│   │   ├── LeadForm.tsx
│   │   ├── ProductFeatures.tsx
│   │   ├── Testimonials.tsx
│   │   ├── TrustBar.tsx
│   │   └── ValueProp.tsx
│   └── ui/
│       ├── Badge.tsx
│       ├── Button.tsx
│       ├── IconByName.tsx
│       ├── StatCard.tsx
│       └── VideoModal.tsx
├── lib/
│   └── content.ts
├── public/
│   └── images/
│       ├── team/
│       └── product/
├── tailwind.config.js
├── tsconfig.json
└── package.json
```

---

## 10. Next Actions

1. **Install dependencies**: `framer-motion`, `react-hook-form`, `zod`, `next-sitemap`.
2. **Build primitives**: `SectionWrapper`, `Button`, `Badge`, `StatCard`.
3. **Enhance Hero**: add `TrustBar`, scroll animations, video placeholder.
4. **Enhance About**: replace avatars, animate timeline.
5. **Enhance Mission**: add background mesh, refine copy.
6. **Build Contact**: `LeadForm` with API integration to Identity gateway.
7. **Add real assets**: team photos, product screenshots, office image.
8. **Production prep**: sitemap, robots.txt, analytics, Lighthouse audit.
