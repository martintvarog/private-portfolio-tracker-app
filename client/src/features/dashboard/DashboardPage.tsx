import { useState } from 'react'
import { HoldingsTable } from '../holdings/HoldingsTable'
import type { Holding } from '../holdings/types'
import { SyncForm } from '../sync/SyncForm'
import type { ConnectorSyncResult } from '../../lib/api'
import { NetWorthCard } from './NetWorthCard'

// Wire → view mapping. CZK cash is valued 1:1; everything else needs
// MarketData prices/FX (step: valuation) and stays unvalued for now.
function toHoldings(result: ConnectorSyncResult): Holding[] {
  const asOf = result.asOf ? new Date(result.asOf).toLocaleString('cs-CZ') : 'just now'
  const label = result.accountLabel ? `${result.source} · ${result.accountLabel}` : result.source

  return result.holdings.map((h) => ({
    sourceId: result.source,
    symbol: h.symbol,
    name: h.name,
    kind: h.kind,
    quantity: h.quantity,
    valueCzk: h.kind === 'cash' && h.currency === 'CZK' ? h.quantity : undefined,
    source: label,
    asOf,
  }))
}

export function DashboardPage() {
  const [holdings, setHoldings] = useState<Holding[]>([])

  const handleSynced = (result: ConnectorSyncResult) => {
    // Re-syncing a source replaces its holdings, never duplicates them.
    setHoldings((previous) => [
      ...previous.filter((h) => h.sourceId !== result.source),
      ...toHoldings(result),
    ])
  }

  const valued = holdings.filter((h) => h.valueCzk !== undefined)
  const total = valued.reduce((sum, h) => sum + (h.valueCzk ?? 0), 0)
  const unvaluedCount = holdings.length - valued.length

  return (
    <main>
      <div className="grid">
        <NetWorthCard totalCzk={total} asOf={new Date().toLocaleString('cs-CZ')} />
        <SyncForm onSynced={handleSynced} />
      </div>
      {unvaluedCount > 0 && (
        <div className="muted" style={{ marginBottom: 12 }}>
          ⚠ {unvaluedCount} holding(s) not valued yet (prices/FX come with MarketData) — the total
          above does not include them.
        </div>
      )}
      <HoldingsTable holdings={holdings} />
    </main>
  )
}
