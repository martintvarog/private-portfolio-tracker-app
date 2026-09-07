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

// The server's per-request id (X-Request-Id header). It identifies the REQUEST in
// the server logs, not the user — a user can quote it in a support message and
// nothing else about them is revealed. Present on every response.
export type SyncResponse = {
  result: ConnectorSyncResult
  requestId: string | null
}

export class SyncError extends Error {
  readonly requestId: string | null
  constructor(message: string, requestId: string | null) {
    super(message)
    this.requestId = requestId
  }
}

export async function postSync(source: string, credential: string): Promise<SyncResponse> {
  const response = await fetch('/api/sync', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ source, credential }),
  })
  const requestId = response.headers.get('X-Request-Id')

  if (!response.ok) {
    // 4xx/5xx here means OUR request/API is broken (ADR-0005); connector
    // outcomes like a dead token arrive as data inside a 200.
    const problem = await response.json().catch(() => null)
    throw new SyncError(problem?.title ?? `Sync failed with HTTP ${response.status}`, requestId)
  }

  return { result: await response.json(), requestId }
}
