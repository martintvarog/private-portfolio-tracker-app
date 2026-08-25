import type { Holding } from './types'

const czk = new Intl.NumberFormat('cs-CZ', { maximumFractionDigits: 0 })

type Props = {
  holdings: Holding[]
}

export function HoldingsTable({ holdings }: Props) {
  return (
    <div className="card">
      <h2>Holdings</h2>
      {holdings.length === 0 && (
        <div className="muted">Nothing here yet — sync a source to see your holdings.</div>
      )}
      <table>
        <thead>
          <tr>
            <th>Asset</th>
            <th>Type</th>
            <th className="num">Quantity</th>
            <th className="num">Value (CZK)</th>
            <th>Source</th>
            <th>As of</th>
          </tr>
        </thead>
        <tbody>
          {holdings.map((holding) => (
            <tr key={`${holding.source}:${holding.symbol}`}>
              <td>
                <strong>{holding.symbol}</strong>
                {holding.name && <span className="muted"> · {holding.name}</span>}
              </td>
              <td>{holding.kind}</td>
              <td className="num">{holding.quantity ?? '—'}</td>
              <td className="num">
                {holding.valueCzk !== undefined ? czk.format(holding.valueCzk) : '—'}
              </td>
              <td>
                <span className="pill">{holding.source}</span>
              </td>
              <td className="muted">{holding.asOf}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
