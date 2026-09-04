# PortfolioTrackerApp

Privacy-first, local-first net-worth and portfolio tracker (working title). The aggregate portfolio never exists server-side: all user state lives in a client-side encrypted vault; server components are stateless or hold only non-personal data (market prices).

## Repository layout

```
├── src/
│   ├── PortfolioTrackerApp.Api/          # host: DI composition root + thin HTTP endpoints (POST /api/sync, GET /health); serves the built client from wwwroot/
│   ├── PortfolioTrackerApp.Domain/       # pure business rules (Money, Asset, Position) — references nothing
│   ├── PortfolioTrackerApp.Connectors/   # capability module: Contracts/ (IConnector) + Fio/ (implemented)
│   └── PortfolioTrackerApp.MarketData/   # capability module: prices & FX (scaffold; not wired yet)
├── tests/                                # xUnit: Domain, Connectors (contract + logging guards), Api (in-process, logging guards)
├── client/                               # React + Vite + TS — owns the aggregate: encrypted vault, valuation
├── docs/adr/                             # architecture decision records — start here for the "why"
├── docs/STATUS.md                        # current state + handoff notes — read second
├── infra/main.bicep                      # all Azure resources; deployed ONLY by the pipeline
├── infra/rbac.bicep                      # role assignments; deployed ONLY by a human
├── Dockerfile                            # multi-stage: build client → publish API → slim runtime image
└── .github/workflows/ci.yml              # build + test, then deploy to Azure on push to main
```

## Architecture in one paragraph

Modular monolith, sliced vertically by capability (ADR-0002). Dependencies point inward: modules reference `Domain`; `Api` references modules only. Each module is ports-and-adapters inside — public `Contracts/`, everything else `internal`. Contracts own their result types, so the domain model is a private implementation detail of the modules: `Api` has no Domain reference and disables transitive project references to keep it that way (ADR-0003). Endpoints stay thin: parse request → call a module contract → map to DTO.

The backend is a stateless specialist: it normalizes institution responses into common terms (Connectors) and serves generic market data (MarketData). Personal data transits per request and is never stored or logged; Postgres holds only instruments, prices and FX rates. The **client is the actual application** — it alone assembles the whole portfolio (synced positions + manual assets × prices), computes snapshots/net worth, and persists everything in an encrypted vault (IndexedDB). The aggregate portfolio never exists server-side.

```
Api ──► Connectors ──► Domain
    ──► MarketData ──► Domain          Api ──► Domain: compile error, by design
```

## API

- `POST /api/sync` — `{ "source": "fio", "credential": "<token>" }` → `ConnectorSyncResult`. One connector per call; the client parallelizes. Connector outcomes (invalid credential, bank down, rate limited) come as `status` inside a 200; HTTP 400 (ProblemDetails) only for malformed requests (ADR-0005); unhandled errors → 500 ProblemDetails with no internals.
- `GET /health`
- Everything else → the built client (`wwwroot`, SPA fallback to `index.html`) — one origin, no CORS (ADR-0006).

## Getting started

Prerequisites: .NET 10 SDK, Node 22+. (Docker only for building the production image.)

```bash
dotnet build                  # backend
dotnet test                   # all tests (Domain, Connectors, Api)
dotnet run --project src/PortfolioTrackerApp.Api    # API on :5018 — GET /health

cd client
npm install
npm run dev                   # Vite dev server on :5173, proxies /api to :5018
```

Production-style run (what the container does): `npm run build`, copy `client/dist/*` into `src/PortfolioTrackerApp.Api/wwwroot/` (gitignored), start the API, open :5018 with Vite off.

```bash
docker build -t portfoliotrackerapp .          # same image the pipeline builds
docker run --rm -p 8080:8080 portfoliotrackerapp
```

## Deployment

Push to `main` → GitHub Actions builds and tests → builds the image → pushes it to Azure Container Registry tagged with the commit SHA → deploys `infra/main.bicep` with that image to Azure Container Apps. No secrets are stored anywhere: the pipeline authenticates with an OIDC federated managed identity, the app pulls images with its own identity (ADR-0007, ADR-0008). Role assignments live in `infra/rbac.bicep` and are deployed by a human only.

Resource names, region, gotchas, and the fresh-subscription bootstrap sequence are in `docs/STATUS.md`.

## Observability

Console output goes to Log Analytics (`ContainerAppConsoleLogs_CL`). Emitted per request: one HttpLogging line (method, path, status, duration — never bodies or headers) and one connector outcome line (`Sync {Source} finished with {Status} in {ElapsedMs} ms`; `Warning` for non-`Ok`). Unhandled exceptions are logged once at `Error`. The law "never log credentials, IBANs, request/response bodies, or outbound URLs" is enforced by `RemoveAllLoggers()` on the bank `HttpClient`, an explicit HttpLogging field allow-list, and tests in `tests/` that fail CI if either is removed.

## Conventions

- Every architecturally significant decision gets an ADR (`docs/adr/template.md`, ADR-0001).
- `Directory.Build.props` applies to all projects: net10.0, nullable enabled, warnings are errors.
- Multi-currency and i18n are first-class from day one.
- Connector secrets are never logged — not in message templates, request URLs, or exception messages (`IConnector` laws; test-guarded).
- `infra/main.bicep` is deployed only by the pipeline; `infra/rbac.bicep` only by a human. Always `what-if` before a manual deploy.
