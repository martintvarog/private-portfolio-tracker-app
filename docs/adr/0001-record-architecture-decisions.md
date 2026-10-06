# ADR-0001: Record architecture decisions

- Status: accepted
- Date: 2026-08-07

## Context

This is a solo project intended both as a product and as a portfolio piece for senior .NET roles. Design decisions (module boundaries, connector trust lanes, privacy invariants) will be made over months and their reasoning will be lost unless written down. Reviewers of the codebase should be able to follow *why*, not just *what*.

## Decision

- **Chosen:** Every architecturally significant decision is recorded as an Architecture Decision Record in `docs/adr`, numbered sequentially, using the format in `0000-adr-template.md` (based on Michael Nygard's ADR format). A decision is architecturally significant if it affects module boundaries, external dependencies, data placement (client vs server), or security/privacy posture.
- **Why:** Design decisions made over months would otherwise lose their reasoning. Reviewers — including future me — need to follow *why*, not just *what*, and this project doubles as an interview portfolio piece where that rationale is itself the evidence of judgement.

## Consequences

Small writing overhead per decision. In exchange: onboarding material for future contributors, interview-ready rationale, and a checklist discipline — deferred features (Degiro CSV, ČS connector, sync) each get an ADR when added, as agreed in the MVP scope.
