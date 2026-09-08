# ADR-0006: One deployable unit — the API serves the built client

- Status: accepted
- Date: 2026-09-01

## Context

In development the React client runs on the Vite dev server (:5173) and proxies `/api` to the ASP.NET API (:5018) — two processes, two origins. For deployment we had to decide what "the app" is as a shippable artifact. Options: (a) two containers, a static-file server for the client and the API, behind a shared ingress; (b) one container, the API serving the compiled client from `wwwroot`.

## Decision

One container. `npm run build` output (`client/dist`) is copied into the API's `wwwroot`; `Program.cs` adds `UseDefaultFiles` + `UseStaticFiles` + `MapFallbackToFile("index.html")`. All API routes live under `/api` (`MapGroup`) so real endpoints always win over the SPA fallback. The Vite dev server remains a development-only tool; production has no Node process.

## Consequences

Same origin for client and API: no CORS, one URL, one TLS certificate, one thing to build, ship, and monitor. Deep links and F5 on client routes work via the fallback. Trade-off: the fallback is a catch-all — a missing asset returns `index.html` (HTML where JS was expected) instead of a clean 404, so asset problems show up as blank pages with MIME errors. Rejected two-container topology: doubles everything to operate and forces cross-origin config for no benefit at this scale; revisit only if the client needs a CDN or independent release cadence.

Amended 2026-09-03 (cache policy): serving the SPA from the API makes the API responsible for the browser cache. `index.html` is served with `Cache-Control: no-cache` (revalidate on every load) and the hashed `assets/*` with `public, max-age=31536000, immutable`, via one `StaticFileOptions.OnPrepareResponse` passed to BOTH `UseStaticFiles` and `MapFallbackToFile` — the fallback runs its own static-file handler and would otherwise serve a cacheable `index.html` for client routes. Without this, users kept running the previous bundle after a deploy until a hard refresh. Test-guarded (`Index_html_is_never_cached…`).
