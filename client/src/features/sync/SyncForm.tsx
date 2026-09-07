import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { postSync, SyncError, type ConnectorSyncResult } from '../../lib/api'

// Connector outcomes are data, not exceptions (ADR-0005) — each gets a message.
const statusMessages: Record<string, string> = {
  InvalidCredential:
    'The institution rejected this credential — it may have expired. Generate a new one and try again.',
  Unavailable: 'The institution is unreachable right now. Try again later.',
  RateLimited: 'Rate limited by the institution (Fio: 1 request per 30 s). Wait and retry.',
}

type Props = {
  onSynced: (result: ConnectorSyncResult, credential: string) => void
  // Prefilled from the vault when this source synced successfully before.
  storedCredential?: string
}

export function SyncForm({ onSynced, storedCredential }: Props) {
  // useState's argument is only the INITIAL value (used on mount) — later
  // changes to storedCredential don't overwrite what the user typed.
  const [token, setToken] = useState(storedCredential ?? '')
  // 'copied' is transient UI state: flips true on click, back to false after 1.5 s.
  const [copied, setCopied] = useState(false)

  const sync = useMutation({
    mutationFn: () => postSync('fio', token),
    onSuccess: ({ result }) => {
      if (result.status === 'Ok') onSynced(result, token)
    },
  })

  // sync.data is now { result, requestId }; the wire result is one level down.
  const result = sync.data?.result
  const outcome = result && result.status !== 'Ok' ? statusMessages[result.status] : null
  // Shown only when something went wrong: lets the user quote a reference that
  // points at exactly their request in the server logs — and nothing else.
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
          event.preventDefault() // stop the browser's full-page form submit
          sync.mutate()
        }}
      >
        <input
          type="password"
          placeholder="Paste your Fio API token"
          value={token}
          onChange={(event) => setToken(event.target.value)}
        />
        <button type="submit" disabled={sync.isPending || token.trim() === ''}>
          {sync.isPending ? 'Syncing…' : 'Sync'}
        </button>
      </form>
      {sync.isError && <div className="sync-error">{sync.error.message}</div>}
      {outcome && <div className="sync-error">{outcome}</div>}
      {failureRef && (
        <div className="muted">
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
          Synced {result.holdings.length} holding(s)
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
