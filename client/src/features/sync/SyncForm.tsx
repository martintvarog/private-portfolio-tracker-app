import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { postSync, SyncError, type ConnectorSyncResult } from '../../lib/api'

const statusMessages: Record<string, string> = {
  InvalidCredential:
    'The institution rejected this credential — it may have expired. Generate a new one and try again.',
  Unavailable: 'The institution is unreachable right now. Try again later.',
  RateLimited: 'Rate limited by the institution (Fio: 1 request per 30 s). Wait and retry.',
}

type Props = {
  onSynced: (result: ConnectorSyncResult, credential: string) => void
  initialCredential?: string
}

export function SyncForm({ onSynced, initialCredential }: Props) {
  const [credential, setCredential] = useState(initialCredential ?? '')
  const [copied, setCopied] = useState(false)

  const sync = useMutation({
    mutationFn: () => postSync('fio', credential),
    onSuccess: ({ result }) => {
      if (result.status === 'Ok') onSynced(result, credential)
    },
  })

  const result = sync.data?.result
  const outcome = result && result.status !== 'Ok' ? statusMessages[result.status] : null
  const failureRef = sync.isError
    ? sync.error instanceof SyncError
      ? sync.error.requestId
      : null
    : outcome
      ? sync.data?.requestId
      : null

  return (
    <div className="card">
      <h2>Sync Fio banka</h2>
      <form
        className="sync-form"
        onSubmit={(event) => {
          event.preventDefault()
          sync.mutate()
        }}
      >
        <input
          type="password"
          placeholder="Paste your Fio API token"
          value={credential}
          onChange={(event) => setCredential(event.target.value)}
        />
        <button type="submit" disabled={sync.isPending || credential.trim() === ''}>
          {sync.isPending ? 'Syncing…' : 'Sync'}
        </button>
      </form>
      {sync.isError && <div className="sync-error">{sync.error.message}</div>}
      {outcome && <div className="sync-error">{outcome}</div>}
      {failureRef && (
        <div className="sync-ref">
          Reference for support:{' '}
          <button
            type="button"
            className="ref-copy"
            title="Copy to clipboard"
            onClick={async () => {
              await navigator.clipboard.writeText(failureRef)
              setCopied(true)
              setTimeout(() => setCopied(false), 1500)
            }}
          >
            <code>{failureRef}</code> {copied ? '✓ copied' : '⧉'}
          </button>
        </div>
      )}
      {result?.status === 'Ok' && (
        <div className="muted">
          Synced {result.positions.length} holding(s)
          {result.accountLabel ? ` from ${result.accountLabel}` : ''}.
          {result.warnings.map((warning) => (
            <div key={warning}>⚠ {warning}</div>
          ))}
        </div>
      )}
      <div className="muted">
        Read-only token, created in Fio internet banking → Settings → API. It is sent straight to
        Fio through the stateless gateway — never stored server-side.
      </div>
    </div>
  )
}
