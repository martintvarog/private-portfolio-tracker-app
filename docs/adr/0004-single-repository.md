# ADR-0004: Single repository for frontend, backend and configuration

- Status: accepted
- Date: 2026-08-10

## Context

The product is one deployable pair (ASP.NET Core API + React client) built by one developer. Most changes touch both sides at once (new endpoint + its consumer). Separate repositories would require cross-repo version pinning, ordered merges, and duplicated CI, and the usual polyrepo drivers (separate teams, release cadences, access control) do not apply. Business/strategy documents must never end up in a public repository.

## Decision

One git repository (`repository/`) holds backend (`src/`, `tests/`), frontend (`client/`), infrastructure (`docker-compose.yml`, `.github/`) and technical docs (`docs/adr/`). Business documents live outside the repository (`../docs`) and are never committed. FE/BE changes ship as atomic commits; a single CI workflow builds and tests both.

## Consequences

Atomic cross-stack changes, one CI, one version history; OpenAPI-based TS type generation stays trivial. CI builds both halves on every push — add `paths:` filters when that becomes slow. Revisit if a piece gains an independent release life (e.g. the planned local agent binary, or a Wealthfolio addon living in an external ecosystem).
