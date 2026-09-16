import { useEffect, useState } from 'react'
import { BrowserRouter, Route, Routes } from 'react-router'
import { AppLayout } from './app/AppLayout'
import { ConnectionsPage } from './features/connections/ConnectionsPage'
import { DashboardPage } from './features/dashboard/DashboardPage'
import { UnlockPage } from './features/vault/UnlockPage'
import { persistVault, vaultExists, type UnlockedVault, type VaultData } from './lib/vault'

function App() {
  // The two pieces of app-level state:
  // - vault: null = locked; the decrypted data + key live ONLY here, in memory
  // - mode: what the unlock screen should offer while vault is null
  const [vault, setVault] = useState<UnlockedVault | null>(null)
  const [mode, setMode] = useState<'loading' | 'create' | 'unlock'>('loading')

  // Runs once, after the first render ([] = no dependencies → never re-runs):
  // ask IndexedDB whether a vault record exists and pick the mode.
  useEffect(() => {
    vaultExists().then((exists) => setMode(exists ? 'unlock' : 'create'))
  }, [])

  // Vault updates flow through here: encrypt-and-save FIRST, then update the
  // in-memory state — if persisting fails, memory never drifts from disk.
  // Note: a NEW object ({ ...vault, data }) — state is replaced, never mutated.
  const updateVaultData = async (data: VaultData) => {
    if (vault === null) return
    const updated = { ...vault, data }
    await persistVault(updated)
    setVault(updated)
  }

  // Three-way render — 'unlocked' is not a mode: it's derived from vault !== null.
  // The router lives INSIDE this branch: no page can match a URL while locked.
  if (vault !== null) {
    return (
      <BrowserRouter>
        <Routes>
          {/* Layout route: no path → renders the shared frame whenever a child matches.
              No matching child (e.g. /unknown) → nothing renders, layout included. */}
          <Route element={<AppLayout />}>
            <Route
              path="/"
              element={<DashboardPage data={vault.data} onDataChange={updateVaultData} />}
            />
            <Route
              path="/connections"
              element={<ConnectionsPage data={vault.data} onDataChange={updateVaultData} />}
            />
            {/* Catch-all: any URL no sibling matched still gets the frame, not a blank page. */}
            <Route path="*" element={<div className="muted">Page not found.</div>} />
          </Route>
        </Routes>
      </BrowserRouter>
    )
  }

  if (mode === 'loading') {
    return <div className="unlock-wrap muted">…</div>
  }

  return <UnlockPage mode={mode} onUnlocked={setVault} />
}

export default App
