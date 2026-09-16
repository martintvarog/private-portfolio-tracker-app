import { HoldingsTable } from '../holdings/HoldingsTable'
import type { Holding } from '../holdings/types'
import type { ConnectorSyncResult } from '../../lib/api'
import type { VaultData } from '../../lib/vault'
import { NetWorthCard } from './NetWorthCard'

type Props = {
  data: VaultData
  onDataChange: (data: VaultData) => void
}

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

// onDataChange is not used yet — syncing moved to the Connections page. The prop
// stays because upcoming dashboard cards (snapshots, display currency) will write.
export function DashboardPage({ data }: Props) {
  // No holdings state here anymore: the vault's raw syncResults are the facts,
  // everything below is derived fresh on every render.
  const holdings = data.syncResults.flatMap(toHoldings)

  const valued = holdings.filter((h) => h.valueCzk !== undefined)
  const total = valued.reduce((sum, h) => sum + (h.valueCzk ?? 0), 0)
  const unvaluedCount = holdings.length - valued.length

  return (
    <div>
      <div className="grid">
        <NetWorthCard totalCzk={total} asOf={new Date().toLocaleString('cs-CZ')} />
      </div>
      {unvaluedCount > 0 && (
        <div className="muted" style={{ marginBottom: 12 }}>
          ⚠ {unvaluedCount} holding(s) not valued yet (prices/FX come with MarketData) — the total
          above does not include them.
        </div>
      )}
      <HoldingsTable holdings={holdings} />
    </div>
  )
}
