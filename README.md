# PortfolioTrackerApp

Privacy-first, local-first net-worth and portfolio tracker (working title). The aggregate portfolio never exists server-side: all user state lives in a client-side encrypted vault; server components are stateless or hold only non-personal data (market prices).

## Repository layout

```
├── src/
│   ├── PortfolioTrackerApp.Api/          # host: DI composition root + thin HTTP endpoints (POST /sync, GET /health)
│   ├── PortfolioTrackerApp.Domain/       # pure business rules (Money, Asset, Position) — references nothing
│   ├── PortfolioTrackerApp.Connectors/   # capability module: Contracts/ (IConnector) + Fio/ (implemented)
│   └── PortfolioTrackerApp.MarketData/   # capability module: prices & FX (Postgres + EF Core)
├── tests/                                # xUnit test projects (Domain, Connectors contract suite)
├── client/                               # React + Vite + TS + TanStack Query — owns the aggregate: vault, snapshots, valuation
├── docs/adr/                             # architecture decision records — start here for the "why"
├── docker-compose.yml                    # local Postgres
└── .github/workflows/ci.yml              # build + test, backend and client
```

## Architecture in one paragraph

Modular monolith, sliced vertically by capability (ADR-0002). Dependencies point inward: modules reference `Domain`; `Api` references modules only. Each module is ports-and-adapters inside — public `Contracts/`, everything else `internal`. Contracts own their result types, so the domain model is a private implementation detail of the modules: `Api` has no Domain reference and disables transitive project references to keep it that way (ADR-0003). Endpoints stay thin: parse request → call a module contract → map to DTO.

The backend is a stateless specialist: it normalizes institution responses into common terms (Connectors) and serves generic market data (MarketData). Personal data transits per request and is never stored or logged; Postgres holds only instruments, prices and FX rates. The **client is the actual application** — it alone assembles the whole portfolio (synced positions + manual assets × prices), computes snapshots/net worth, and persists everything in an encrypted vault (IndexedDB). The aggregate portfolio never exists server-side.

```
Api ──► Connectors ──► Domain
    ──► MarketData ──► Domain          Api ──► Domain: compile error, by design
```

## API

- `POST /sync` — `{ "source": "fio", "credential": "<token>" }` → `ConnectorSyncResult`. One connector per call; the client parallelizes. Connector outcomes (invalid credential, bank down, rate limited) come as `status` inside a 200; HTTP 400 (ProblemDetails) only for malformed requests (ADR-0005).
- `GET /health`

## Getting started

Prerequisites: .NET 10 SDK, Node 22+, Docker.

```bash
docker compose up -d          # Postgres 17 on :5432
dotnet build                  # backend
dotnet test                   # backend tests
dotnet run --project src/PortfolioTrackerApp.Api    # API — GET /health

cd client
npm install
npm run dev                   # Vite dev server
```

## Conventions

- Every architecturally significant decision gets an ADR (`docs/adr/template.md`, ADR-0001).
- `Directory.Build.props` applies to all projects: net10.0, nullable enabled, warnings are errors.
- Multi-currency and i18n are first-class from day one; connector secrets are never logged.
