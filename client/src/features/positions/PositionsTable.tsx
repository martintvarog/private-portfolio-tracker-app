import type { Position } from './types'

const czk = new Intl.NumberFormat('cs-CZ', { maximumFractionDigits: 0 })

type Props = {
  positions: Position[]
}

// Lists positions (one row per source); the UI calls them "Holdings" until the
// aggregated Holding exists (docs/glossary.md).
export function PositionsTable({ positions }: Props) {
  return (
    <div className="card">
      <h2>Holdings</h2>
      {positions.length === 0 && (
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
          {positions.map((position) => (
            <tr key={`${position.connectionLabel}:${position.symbol}`}>
              <td>
                <strong>{position.symbol}</strong>
                {position.name && <span className="muted"> · {position.name}</span>}
              </td>
              <td>{position.assetType}</td>
              <td className="num">{position.quantity ?? '—'}</td>
              <td className="num">
                {position.displayValue !== undefined ? czk.format(position.displayValue) : '—'}
              </td>
              <td>
                <span className="pill">{position.connectionLabel}</span>
              </td>
              <td className="muted">{position.asOf}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
