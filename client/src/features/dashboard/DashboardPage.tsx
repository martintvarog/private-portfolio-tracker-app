import { PositionsTable } from '../positions/PositionsTable'
import type { Position } from '../positions/types'
import type { ConnectorSyncResult } from '../../lib/api'
import type { VaultData } from '../../lib/vault'
import { NetWorthCard } from './NetWorthCard'

type Props = {
  data: VaultData
  onDataChange: (data: VaultData) => void
}

// Wire → view mapping. CZK cash is valued 1:1; everything else needs
// MarketData prices/FX (step: valuation) and stays unvalued for now.
function toPositions(result: ConnectorSyncResult): Position[] {
  const asOf = result.asOf ? new Date(result.asOf).toLocaleString('cs-CZ') : 'just now'
  const connectionLabel = result.accountLabel
    ? `${result.source} · ${result.accountLabel}`
    : result.source

  return result.positions.map((p) => ({
    source: result.source,
    symbol: p.symbol,
    name: p.name,
    assetType: p.assetType,
    quantity: p.quantity,
    displayValue: p.assetType === 'cash' && p.currency === 'CZK' ? p.quantity : undefined,
    connectionLabel,
    asOf,
  }))
}

// onDataChange is not used yet — syncing moved to the Connections page. The prop
// stays because upcoming dashboard cards (snapshots, display currency) will write.
export function DashboardPage({ data }: Props) {
  // No positions state here anymore: the vault's raw syncResults are the facts,
  // everything below is derived fresh on every render.
  const positions = data.syncResults.flatMap(toPositions)

  const valued = positions.filter((p) => p.displayValue !== undefined)
  const netWorth = valued.reduce((sum, p) => sum + (p.displayValue ?? 0), 0)
  const unvaluedCount = positions.length - valued.length

  return (
    <div>
      <div className="grid">
        <NetWorthCard netWorth={netWorth} asOf={new Date().toLocaleString('cs-CZ')} />
      </div>
      {unvaluedCount > 0 && (
        <div className="muted" style={{ marginBottom: 12 }}>
          ⚠ {unvaluedCount} holding(s) not valued yet (prices/FX come with MarketData) — the total
          above does not include them.
        </div>
      )}
      <PositionsTable positions={positions} />
    </div>
  )
}
