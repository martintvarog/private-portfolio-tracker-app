// Wire types — mirror the backend Connectors contracts (ADR-0003).
// Kept separate from the client domain on purpose: the wire shape is the
// backend's; what the app *means* by a holding is the client's.

export type SyncStatus = 'Ok' | 'InvalidCredential' | 'Unavailable' | 'RateLimited'

export type SyncedHolding = {
  kind: 'cash' | 'security' | 'crypto' | 'other'
  symbol: string
  quantity: number
  currency: string
  name?: string
  isin?: string
}

export type ConnectorSyncResult = {
  source: string
  status: SyncStatus
  accountLabel?: string
  asOf?: string
  holdings: SyncedHolding[]
  warnings: string[]
}

export async function postSync(source: string, credential: string): Promise<ConnectorSyncResult> {
  const response = await fetch('/api/sync', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ source, credential }),
  })

  if (!response.ok) {
    // 4xx/5xx here means OUR request/API is broken (ADR-0005); connector
    // outcomes like a dead token arrive as data inside a 200.
    const problem = await response.json().catch(() => null)
    throw new Error(problem?.title ?? `Sync failed with HTTP ${response.status}`)
  }

  return response.json()
}
