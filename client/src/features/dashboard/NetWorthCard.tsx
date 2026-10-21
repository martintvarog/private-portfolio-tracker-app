const czk = new Intl.NumberFormat('cs-CZ', { maximumFractionDigits: 0 })

type Props = {
  netWorth: number
  asOf: string
  netWorthDelta?: number
}

export function NetWorthCard({ netWorth, asOf, netWorthDelta }: Props) {
  return (
    <div className="card">
      <h2>Total net worth</h2>
      <div className="networth-value">
        {czk.format(netWorth)} <span className="networth-currency">CZK</span>
      </div>
      {netWorthDelta !== undefined && (
        <div className={netWorthDelta >= 0 ? 'delta-up' : 'delta-down'}>
          {netWorthDelta >= 0 ? '▲ +' : '▼ '}
          {czk.format(netWorthDelta)} CZK since last snapshot
        </div>
      )}
      <div className="muted">Computed on this device · {asOf}</div>
    </div>
  )
}
