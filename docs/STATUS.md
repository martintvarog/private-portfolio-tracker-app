# Project status & handoff

> Snapshot for anyone (human or Claude session) picking this up.
> Last updated: 2026-09-02. Update this file when a work block completes.

## Process — read first

STRICT mentor mode is in force (top-level `CLAUDE.md`, Martin's explicit request):
concept → ONE guiding question → wait for Martin's attempt. No unprompted
code/config, no batching ahead. Martin is senior-track .NET, junior FE — explain
React/TS/infra fundamentals, quiz understanding before advancing. Deleted work
is acceptable; the learning is the deliverable.

## What works today (walking skeleton COMPLETE + vault)

- **Backend** (`src/`): modular monolith. `POST /api/sync` (one connector per
  call, outcomes as data — ADR-0005; `/api` prefix via `MapGroup` in
  `Program.cs`), `GET /health` at root. FioConnector done (WireMock-tested).
  Enums serialize as strings. 25 tests green (`dotnet test`).
- **Client** (`client/`): React+Vite+TS. Full flow works end-to-end:
  create/unlock **encrypted vault** (PBKDF2 600k + AES-GCM, ONE sealed blob in
  IndexedDB — `lib/crypto.ts`, `lib/db.ts`, `lib/vault.ts`) → dashboard →
  sync Fio with real token → holdings + total → credential saved to vault
  (on-success only) → F5 survives, token prefilled. Dashboard owns no state:
  everything derives from `vault.data.syncResults` (raw per-source results).
  Valuation: CZK cash only 1:1; everything else shows "—" + honest banner
  (needs MarketData prices/FX).
- **CI**: `.github/workflows/ci.yml` — backend build+test, client build. Green.
- **Dev run**: `dotnet run --project src/PortfolioTrackerApp.Api` (:5018) +
  `cd client && npm run dev` (:5173, proxies `/api` unchanged).

## In flight: deployment ladder (Martin's guided exercise)

Decision made: **one container** — API serves the built client from `wwwroot`
(same origin, no CORS; ADR candidate). A complete Docker rung was built by
Claude on 2026-09-01 and then **deliberately deleted** so Martin can rebuild it
step-by-step. Do NOT skip ahead; guide him through:

1. ~~`/api` route prefix~~ ✔ done, committed (`fix path`).
2. **NEXT →** API serves static files: `UseDefaultFiles` + `UseStaticFiles` +
   `MapFallbackToFile("index.html")` in `Program.cs`; verify by copying
   `client/dist` into `wwwroot` and opening :5018 directly.
3. Multi-stage `Dockerfile` (node:22-alpine builds client → sdk:10.0 publishes
   API → aspnet:10.0 runtime + `dist`→`wwwroot`). Reference version existed at
   commit-time 2026-09-01 chat; ~231 MB image, smoke-tested OK.
4. `docker-compose.yml`: add `app` service (build: ., 8080:8080).
   ⚠ NOTE: `docker-compose.yml` is currently DELETED in the working tree
   (uncommitted `D`) — ask Martin if intentional; it held the Postgres service.
5. Azure Container Apps + Bicep + OIDC-federated GitHub deploy (paper §7.4).

## Next after deployment (order per roadmap §8 / backlog)

- Manual assets (pure client-side vault CRUD) or FX + non-CZK valuation
  (first MarketData feature: ČNB rates). Then snapshots history → Phase 2.5
  goals. See `docs/backlog.md` (repo) for the full feature list + design rules.

## Documentation map

- `CLAUDE.md` (repo root above) — mentor protocol.
- `README.md` — architecture overview (accurate as of this date).
- `docs/adr/` 0002–0005 (+0001 in top-level `docs/adr/`). **ADR gaps** (Martin
  writes ADRs): vault crypto design; one-container topology + `/api` prefix;
  enums-as-strings.
- `repository/docs/backlog.md` — features by data dependency.
- top-level `docs/business-technical-paper.html` — §8 roadmap current
  (2026-08-23); §6.1/§7.2/§7.3 + meta block known-stale (rev.3 offered, pending).
- Miro board (architecture + UI v2 mockups): https://miro.com/app/board/uXjVHz8auJM=/
