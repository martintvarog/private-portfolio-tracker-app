import type { ConnectorSyncResult } from '../../lib/api'
import type { Connection, VaultData } from '../../lib/vault'
import { SyncForm } from '../sync/SyncForm'
import { ConnectionCard } from './ConnectionCard'

type Props = {
  data: VaultData
  onDataChange: (data: VaultData) => void
}

export function ConnectionsPage({ data, onDataChange }: Props) {
  // One card per stored connection, joined at render time to the sync result of
  // the same source. Same derived-view idea as positions.
  const cards = data.connections.map((connection) => ({
    connection,
    result: data.syncResults.find((r) => r.source === connection.source),
  }))

  const handleSynced = (result: ConnectorSyncResult, credential: string) => {
    // Re-syncing a source replaces its results and its stored connection
    // (matched by source + account), never duplicates them. The connection
    // reaches the vault only here — i.e. only after a successful sync.
    onDataChange({
      syncResults: [...data.syncResults.filter((r) => r.source !== result.source), result],
      connections: [
        ...data.connections.filter(
          (c) => !(c.source === result.source && c.accountLabel === result.accountLabel),
        ),
        { source: result.source, credential, accountLabel: result.accountLabel },
      ],
    })
  }

  const handleRemove = (connection: Connection) => {
    // Martin's decision (2026-09-16): removing a connection also drops its holdings,
    // so nothing stale lingers with no way to refresh it. No undo → confirm first.
    if (!window.confirm(`Remove this connection? Its holdings disappear from the dashboard.`)) {
      return
    }
    onDataChange({
      syncResults: data.syncResults.filter((r) => r.source !== connection.source),
      connections: data.connections.filter(
        (c) => !(c.source === connection.source && c.accountLabel === connection.accountLabel),
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

      {cards.length === 0 && (
        <div className="card slot" style={{ marginBottom: 16 }}>
          No connections yet — add one below.
        </div>
      )}
      <div className="grid">
        {cards.map(({ connection, result }) => (
          <ConnectionCard
            key={`${connection.source}:${connection.accountLabel ?? ''}`}
            connection={connection}
            result={result}
            onRemove={() => handleRemove(connection)}
          />
        ))}
      </div>

      <SyncForm
        onSynced={handleSynced}
        initialCredential={data.connections.find((c) => c.source === 'fio')?.credential}
      />
    </>
  )
}
