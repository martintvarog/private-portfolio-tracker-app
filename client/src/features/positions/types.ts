// Temporary view type for the walking skeleton. The real client domain
// (Position, Snapshot, valuation) replaces this in a later step — Martin's code.
export type Position = {
  source: string // source id, e.g. "fio" — used to replace on re-sync
  symbol: string
  name?: string
  assetType: 'cash' | 'security' | 'crypto' | 'other'
  quantity?: number
  // Undefined = we can't value it yet (needs MarketData prices/FX).
  // Skip loudly: the table shows "—" and the total says what it's missing.
  displayValue?: number
  connectionLabel: string // display text, e.g. "fio · CZ65…"
  asOf: string
}
