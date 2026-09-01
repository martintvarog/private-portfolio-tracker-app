import { useState } from 'react'
import type { SubmitEvent } from 'react'
import { createVault, unlockVault, type UnlockedVault } from '../../lib/vault'

type Props = {
  mode: 'create' | 'unlock'
  onUnlocked: (vault: UnlockedVault) => void
}

export function UnlockPage({ mode, onUnlocked }: Props) {
  const [passphrase, setPassphrase] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  const handleSubmit = async (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault()
    setPending(true)
    setError(null)
    try {
      const vault = mode === 'create' ? await createVault(passphrase) : await unlockVault(passphrase)
      onUnlocked(vault)
    } catch {
      // GCM's failed tag check lands here — the only way to be wrong is the passphrase.
      setError(mode === 'create' ? 'Could not create the vault.' : 'Wrong passphrase.')
    } finally {
      setPending(false)
    }
  }

  const buttonLabel = pending ? 'Deriving key…' : mode === 'create' ? 'Create vault' : 'Unlock vault'

  return (
    <div className="unlock-wrap">
      <form className="card unlock-card" onSubmit={handleSubmit}>
        <div className="unlock-logo">🗝️</div>
        <h1>Portfolio Tracker</h1>
        <div className="muted unlock-sub">Local, encrypted, yours.</div>

        <input
          type="password"
          placeholder={mode === 'create' ? 'Choose a passphrase' : 'Passphrase'}
          value={passphrase}
          onChange={(event) => setPassphrase(event.target.value)}
          autoFocus
        />

        {error && <div className="sync-error">{error}</div>}

        <button type="submit" disabled={pending || passphrase.trim() === ''}>
          {buttonLabel}
        </button>

        <ul className="unlock-notes">
          <li>🔒 Your portfolio is stored encrypted in this browser, with a key derived from your passphrase.</li>
          <li>🛡️ The passphrase — and your aggregated wealth — never leave this device.</li>
          <li>⚠️ No recovery. We can't reset what we can't see — keep the passphrase safe.</li>
        </ul>

        {mode === 'create' && (
          <div className="muted">New here? Your vault is created on this device when you continue.</div>
        )}
      </form>
    </div>
  )
}
