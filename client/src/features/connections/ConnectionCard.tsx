import type { ConnectorSyncResult, SyncStatus } from '../../lib/api'
import type { Connection } from '../../lib/vault'

// Display names for source ids; unknown ids fall back to the raw id.
const sourceNames: Record<string, string> = { fio: 'Fio banka' }

// One label + css class per outcome. `undefined` = connection saved but no result yet.
const statusChips: Record<SyncStatus, { label: string; className: string }> = {
  Ok: { label: '✓ OK', className: 'chip chip-ok' },
  InvalidCredential: { label: '✕ Invalid credential', className: 'chip chip-err' },
  Unavailable: { label: 'Unreachable', className: 'chip chip-err' },
  RateLimited: { label: '⏱ Rate limited', className: 'chip chip-warn' },
}

type Props = {
  connection: Connection
  // The sync result for the same source, if any. Joined by the parent at render time.
  result?: ConnectorSyncResult
  onRemove: () => void
}

export function ConnectionCard({ connection, result, onRemove }: Props) {
  const chip = result ? statusChips[result.status] : { label: 'not synced', className: 'chip' }
  const lastSync = result?.asOf ? new Date(result.asOf).toLocaleString('cs-CZ') : '—'

  return (
    <div className="card">
      <div className="card-head">
        <h2>{sourceNames[connection.source] ?? connection.source}</h2>
        <span className="pill">Direct · your API token</span>
        <span className={chip.className}>{chip.label}</span>
      </div>
      <dl className="facts">
        <dt>Account</dt>
        <dd>{connection.accountLabel ?? '—'}</dd>
        <dt>Last sync</dt>
        <dd>{lastSync}</dd>
      </dl>
      {result?.warnings.map((warning) => (
        <div key={warning} className="muted">
          ⚠ {warning}
        </div>
      ))}
      <div className="card-actions">
        <button type="button" className="btn-secondary" onClick={onRemove}>
          Remove
        </button>
      </div>
    </div>
  )
}
