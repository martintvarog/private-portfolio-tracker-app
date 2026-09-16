import type { ConnectorSyncResult, SyncStatus } from '../../lib/api'
import type { StoredCredential } from '../../lib/vault'

// Display names for connector ids; unknown ids fall back to the raw id.
const sourceNames: Record<string, string> = { fio: 'Fio banka' }

// One label + css class per outcome. `undefined` = credential saved but no result yet.
const statusChips: Record<SyncStatus, { label: string; className: string }> = {
  Ok: { label: '✓ OK', className: 'chip chip-ok' },
  InvalidCredential: { label: '✕ Invalid credential', className: 'chip chip-err' },
  Unavailable: { label: 'Unreachable', className: 'chip chip-err' },
  RateLimited: { label: '⏱ Rate limited', className: 'chip chip-warn' },
}

type Props = {
  credential: StoredCredential
  // The sync result for the same source, if any. Joined by the parent at render time.
  result?: ConnectorSyncResult
  onRemove: () => void
}

export function ConnectionCard({ credential, result, onRemove }: Props) {
  const chip = result ? statusChips[result.status] : { label: 'not synced', className: 'chip' }
  const lastSync = result?.asOf ? new Date(result.asOf).toLocaleString('cs-CZ') : '—'

  return (
    <div className="card">
      <div className="card-head">
        <h2>{sourceNames[credential.source] ?? credential.source}</h2>
        <span className="pill">Direct · your API token</span>
        <span className={chip.className}>{chip.label}</span>
      </div>
      <dl className="facts">
        <dt>Account</dt>
        <dd>{credential.accountLabel ?? '—'}</dd>
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
