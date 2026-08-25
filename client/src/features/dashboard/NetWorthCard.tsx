const czk = new Intl.NumberFormat('cs-CZ', { maximumFractionDigits: 0 })

type Props = {
  totalCzk: number
  asOf: string
  deltaCzk?: number
}

export function NetWorthCard({ totalCzk, asOf, deltaCzk }: Props) {
  return (
    <div className="card">
      <h2>Total net worth</h2>
      <div className="networth-value">
        {czk.format(totalCzk)} <span className="networth-currency">CZK</span>
      </div>
      {deltaCzk !== undefined && (
        <div className={deltaCzk >= 0 ? 'delta-up' : 'delta-down'}>
          {deltaCzk >= 0 ? '▲ +' : '▼ '}
          {czk.format(deltaCzk)} CZK since last snapshot
        </div>
      )}
      <div className="muted">Computed on this device · {asOf}</div>
    </div>
  )
}
