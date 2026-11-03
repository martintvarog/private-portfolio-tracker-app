import type { ConnectorSyncResult } from '../../lib/api'
import type { Connection, VaultData } from '../../lib/vault'
import { SyncForm } from '../sync/SyncForm'
import { ConnectionCard } from './ConnectionCard'

type Props = {
  data: VaultData
  onDataChange: (data: VaultData) => void
}

export function ConnectionsPage({ data, onDataChange }: Props) {
  const cards = data.connections.map((connection) => ({
    connection,
    result: data.syncResults.find((r) => r.source === connection.source),
  }))

  const handleSynced = (result: ConnectorSyncResult, credential: string) => {
    // The only place a credential is saved: after a successful sync (ADR-0009).
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
