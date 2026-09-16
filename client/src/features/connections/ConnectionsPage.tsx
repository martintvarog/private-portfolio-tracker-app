import type { ConnectorSyncResult } from '../../lib/api'
import type { StoredCredential, VaultData } from '../../lib/vault'
import { SyncForm } from '../sync/SyncForm'
import { ConnectionCard } from './ConnectionCard'

type Props = {
  data: VaultData
  onDataChange: (data: VaultData) => void
}

export function ConnectionsPage({ data, onDataChange }: Props) {
  // A "connection" is not stored: it's one card per credential, joined at render
  // time to the sync result of the same source. Same derived-view idea as holdings.
  const connections = data.credentials.map((credential) => ({
    credential,
    result: data.syncResults.find((r) => r.source === credential.source),
  }))

  const handleSynced = (result: ConnectorSyncResult, credential: string) => {
    // Re-syncing a source replaces its results and its stored credential
    // (matched by source + account), never duplicates them. The credential
    // reaches the vault only here — i.e. only after a successful sync.
    onDataChange({
      syncResults: [...data.syncResults.filter((r) => r.source !== result.source), result],
      credentials: [
        ...data.credentials.filter(
          (c) => !(c.source === result.source && c.accountLabel === result.accountLabel),
        ),
        { source: result.source, credential, accountLabel: result.accountLabel },
      ],
    })
  }

  const handleRemove = (credential: StoredCredential) => {
    // Martin's decision (2026-09-16): removing a connection also drops its holdings,
    // so nothing stale lingers with no way to refresh it. No undo → confirm first.
    if (!window.confirm(`Remove this connection? Its holdings disappear from the dashboard.`)) {
      return
    }
    onDataChange({
      syncResults: data.syncResults.filter((r) => r.source !== credential.source),
      credentials: data.credentials.filter(
        (c) => !(c.source === credential.source && c.accountLabel === credential.accountLabel),
      ),
    })
  }

  return (
    <>
      <h1>Connections</h1>
      <div className="muted" style={{ marginBottom: 16 }}>
        Each connection syncs one account. Credentials live only in your encrypted vault on this
        device. On sync, your credential goes straight to the institution through the stateless
        gateway and is forgotten — nothing stored or logged server-side.
      </div>

      {connections.length === 0 && (
        <div className="card slot" style={{ marginBottom: 16 }}>
          No connections yet — add one below.
        </div>
      )}
      <div className="grid">
        {connections.map(({ credential, result }) => (
          <ConnectionCard
            key={`${credential.source}:${credential.accountLabel ?? ''}`}
            credential={credential}
            result={result}
            onRemove={() => handleRemove(credential)}
          />
        ))}
      </div>

      <SyncForm
        onSynced={handleSynced}
        storedCredential={data.credentials.find((c) => c.source === 'fio')?.credential}
      />
    </>
  )
}
