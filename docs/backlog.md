# Feature backlog

Features out of the walking skeleton, with the design rules that keep them cheap
to add. Each gets its own ADR when picked up. Source: Martin's Excel (2026-08-21)
+ product vision.

## Grouped by the data they need

### A. Needs nothing new (client-side over existing data)

- **Display-currency toggle** — see whole net worth in CZK, one click → EUR,
  without changing settings. *Candidate to pull into MVP: the domain valuation
  API must take `displayCurrency` as a parameter from day one either way.*
- **Hierarchical allocation goals** — target % at three levels:
  net worth → category (30 % ETF) → within category (50 % of ETF in global,
  30 % in S&P 500). Per level: target %, actual %, target in currency,
  difference in currency AND %  (the rebalancing table).
- **Free capital to invest** — cash reserve vs what goals say should be deployed.

### B. Needs snapshot history (vault snapshots — already in MVP plan)

- **Period evaluation** — quarterly/yearly portfolio review with graph:
  per category, in %, in currency, and against invested capital.
- **Net-worth history chart** on dashboard.

### C. Needs transactions / cost basis (the P&L layer)

- **Capital invested per asset** (cost basis).
- **Totals row**: sum invested, whole realized capital, whole unrealized
  (capital + P&L), free capital.
- **Realized earnings** per product AND per tax period (calendar year — CZ tax
  context: time test / limits will matter, own ADR).
- **Date of last buy per asset** (also feeds the DCA helper).

Design rules in force NOW so this layer stays an add, not a rewrite:

1. **Connector contract** — transactions arrive as a new capability (separate
   method/endpoint), never by reshaping `SyncedHolding` (ADR-0003: additive
   contract evolution).
2. **Client domain** — `Position` = "what you hold now", never carries cost.
   Cost basis lands later as separate `Transaction`/`Lot` concepts that
   valuation consumes optionally.
3. **Vault** — IndexedDB schema versioned from day one; raw per-source sync
   results stored (not only the merged aggregate) so history can be re-derived.

### D. Needs goals + transactions combined

- **DCA helper** — per product: desired share + buy period (e.g. monthly).
  When a period elapses since last buy → overdue indicator (red) / notification:
  "you should buy X for ~Y CZK" (derived from goal difference).

### E. Needs instrument metadata (MarketData enrichment)

- **Insights page ("AI helper")** — whole net worth in detail with sub-tabs per
  category (ETF: composition by country/market/sector). Surfaces factual tilts
  ("90 % of ETF exposure is US market"). Explicitly NOT an AI portfolio builder:
  real data in context, no opinions. Requires ETF composition/exposure data as a
  MarketData capability.

### F. Far future

- **Automated DCA execution (end-to-end)** — extends the DCA helper (D) from
  "tell me what to buy" to "do it for me". Target flow, per Martin (2026-09-03):
  1. user grants API credentials once; 2. app derives the target buy from the
  goal gap (D); 3. app transfers the cash from the user's bank account to the
  broker; 4. app places the buy order at the broker; 5. app syncs the portfolio
  (already MVP) and records the buy as the new "last buy" (C).

  Per-step feasibility as of 2026-09-03:
  - Steps 1–2, 5: covered by existing plans (goals, DCA helper, transactions,
    sync). No new lane needed.
  - Step 3 (bank → broker payment): initiating payments on a user's behalf is a
    PISP activity under PSD2 — needs own licence or a licensed aggregator.
    Self-service exception: Fio token API supports payment import when the
    token has write rights (user-owned account, no third-party licence). ČS
    Final API Consumer: payment scope to be verified.
  - Step 4 (broker order): IBKR Flex Query is read-only by design; orders need
    the Client Portal Web API (gateway session + 2FA, session expiry is the
    operational pain). Degiro: no API at all — lane impossible, CSV stays
    read-only. Crypto exchanges: order APIs exist (Kraken, Coinbase) with
    scoped keys — likely the cheapest first experiment.

  Rules in force NOW: every credential the app asks for stays READ-ONLY;
  execution gets a separate, explicitly-consented write-scope credential lane
  (own trust label, own ADR). Design-for: `IConnector` credential lifecycle must
  be able to express scope (read vs write) so a write lane is an add, not a
  rewrite. Prerequisites: P&L/transactions layer (C) for last-buy dates, goals
  (A/D) for the target amount. Regulatory + trust minefield — stays far future.
- Aggregator connector lane · encrypted multi-device sync · local agent
  ("purist mode") · wealth graph (asset↔liability↔cash-flow links) · rental
  module (rent, indexation, vyúčtování) · AI valuations of illiquid assets.

## UI-structure consequences (decide before coding the shell)

- Goals/DCA and Insights are first-class nav sections → nav must scale to ~7
  items (sidebar candidate).
- Holdings rows are entities (future per-position detail: cost, last buy,
  realized) → rows clickable, stable position identity.
- Dashboard = grid of self-contained cards → future features add cards, not
  layout rework.
- Every money figure rendered through one "display currency" mechanism (A).
