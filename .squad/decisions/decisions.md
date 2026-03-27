# Decisions

## 2026-03-27: Companies API Design

**Author:** Hicks (Backend Dev)  
**Status:** Implemented

### What
Company entity added to CRM with full CRUD API. Contact.CompanyId is a nullable FK. Company name is controlled vocabulary — contacts must select from the companies table, not free-text. GET /api/companies/search?q= powers the UI autocomplete. Deleting a company with linked contacts returns 409 Conflict.

### API Surface
- GET /api/companies — paged list
- GET /api/companies/search?q= — autocomplete (≤20 results)
- GET /api/companies/{id} — detail with contacts
- POST /api/companies — create
- PUT /api/companies/{id} — update
- DELETE /api/companies/{id} — 409 if has contacts

---

## 2026-03-26: WarpBusiness.MarketingSite — Static Advertising Website

**Author:** Vasquez (Frontend Dev)  
**Status:** Implemented

### Context
The product needs a public-facing marketing/advertising site at warp-business.com. This is separate from the Aspire-orchestrated application stack and requires no backend, authentication, or database.

### Decision
Create `src/WarpBusiness.MarketingSite/` as a standalone static HTML/CSS/JS site served by a minimal ASP.NET Core host (`net10.0`, `Microsoft.NET.Sdk.Web`), using only `UseDefaultFiles()` and `UseStaticFiles()`. All static assets live in `wwwroot/`.

### Key Design Choices
- **No external CSS frameworks** — custom CSS only, using CSS custom properties for the full color scheme
- **Fonts:** Orbitron (futuristic headings) + Inter (body) via Google Fonts
- **Theme:** Dark space aesthetic — deep navy (`#050b18`), electric blue/cyan (`#00c8ff`) accents, white text
- **Interactivity:** Vanilla JS only — animated star-field canvas (requestAnimationFrame), IntersectionObserver scroll-reveal, smooth-scroll anchors, mobile hamburger menu
- **Not in Aspire AppHost** — marketing site is independent; no service discovery or orchestration needed

### Sections
1. Sticky nav (logo + links + CTA button)
2. Full-viewport hero — "When Business Moves at Warp Speed" + animated star-field
3. Features grid — CRM, Employee Management, Customer Portal, Plugin Architecture
4. Stats bar — "Business at Warp Speed" / 10x Faster / 360° Visibility / ∞ Extensible
5. CTA — "When Business Moves Faster" / Get Started Free
6. Footer — © 2026 Warp Business / warp-business.com

### Trade-offs
- Pure static files means zero server-side logic; SEO relies on static HTML (sufficient for marketing)
- Google Fonts loaded via CDN — small dependency, acceptable for a marketing site
- Canvas star-field degrades gracefully (canvas hidden if JS disabled; content still fully readable)

### Project Location
`src/WarpBusiness.MarketingSite/` added to `src/WarpBusiness.slnx`

---

## 2026-03-27: Plugin Showcase Maintenance Convention

**Author:** Michael R. Schmidt (via Vasquez)  
**Status:** Active Convention

### What
When a new plugin project is added to the WarpBusiness solution, it must be added to the rotating plugin showcase on the marketing site (src/WarpBusiness.MarketingSite/wwwroot/js/main.js — plugins array). The Sample plugin (WarpBusiness.Plugin.Sample) is excluded — it is a developer scaffold template, not a product feature.

### Why
Keep the marketing site current with the product's plugin ecosystem automatically.
