export type SyncStatus = 'Ok' | 'InvalidCredential' | 'Unavailable' | 'RateLimited'

export type SyncedPosition = {
  assetType: 'cash' | 'security' | 'crypto' | 'other'
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
  positions: SyncedPosition[]
  warnings: string[]
}

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
    const problem = await response.json().catch(() => null)
    throw new SyncError(problem?.title ?? `Sync failed with HTTP ${response.status}`, requestId)
  }

  return { result: await response.json(), requestId }
}
