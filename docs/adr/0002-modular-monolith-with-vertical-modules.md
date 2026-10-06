# ADR-0002: Modular monolith with vertical capability modules

- Status: accepted
- Date: 2026-08-10

## Context

The backend needs clean architecture: business rules isolated from delivery and infrastructure details, dependencies pointing inward. The textbook layering (Domain / Application / Infrastructure / Presentation as horizontal solution-wide projects) keeps use cases in one place, but in practice the single Infrastructure project becomes a junk drawer (bank HTTP clients next to EF Core repositories) and every feature smears across three projects. We are one developer building capabilities that differ strongly in their infrastructure needs (connectors: HTTP + credential handling; market data: Postgres + caching). Microservices would give the same capability boundaries at a heavy operational cost we cannot justify.

## Decision

- **Chosen:** A modular monolith sliced vertically by business capability:
  - `Domain` — innermost ring: value objects and entities (Money, Asset, Position, Snapshot). References nothing, no NuGet packages. Anything unit-testable without mocks belongs here.
  - `Connectors`, `MarketData` (more to come, e.g. `Portfolio`) — one project per capability. Each module carries its own application layer (use cases) and infrastructure (adapters) internally, following ports-and-adapters *inside* the module. Only `Contracts/` is public; everything else is `internal`.
  - `Api` — outermost ring and composition root: DI wiring and thin HTTP endpoints. The only project that references all modules.
  - Cross-module use cases: pure call-A-then-B coordination may live in Api; the moment it contains a business decision it gets its own orchestrating module (expected: `Portfolio` for snapshot/net-worth computation).
- **Why:** Horizontal layering (Domain/Application/Infrastructure/Presentation as solution-wide projects) turns the one Infrastructure project into a junk drawer once capabilities need very different infrastructure (connectors need HTTP + credential handling, market data needs Postgres + caching), and every feature ends up smeared across three projects. Microservices would give the same capability boundaries but at an operational cost a solo developer can't justify. Vertical slicing keeps each capability's infrastructure choices local to it, without paying for service boundaries not needed yet.

## Consequences

High cohesion per capability and per-module infrastructure freedom; module boundaries double as future service boundaries if scale ever demands extraction. A second delivery host (planned local agent / "purist mode" binary) can compose the same modules. **Risk:** no single solution-wide use-case layer — the discipline of "endpoints stay thin" is partly on us until architecture tests (e.g. NetArchTest) are added. Revisit if cross-module orchestration grows beyond a dedicated Portfolio module.
