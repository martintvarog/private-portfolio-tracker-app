import { useEffect, useState } from 'react'
import { BrowserRouter, Route, Routes } from 'react-router'
import { AppLayout } from './app/AppLayout'
import { ConnectionsPage } from './features/connections/ConnectionsPage'
import { DashboardPage } from './features/dashboard/DashboardPage'
import { UnlockPage } from './features/vault/UnlockPage'
import { persistVault, vaultExists, type UnlockedVault, type VaultData } from './lib/vault'

function App() {
  const [vault, setVault] = useState<UnlockedVault | null>(null)
  const [mode, setMode] = useState<'loading' | 'create' | 'unlock'>('loading')

  useEffect(() => {
    vaultExists().then((exists) => setMode(exists ? 'unlock' : 'create'))
  }, [])

  // Persist before setVault: if saving fails, memory must not drift from disk.
  const updateVaultData = async (data: VaultData) => {
    if (vault === null) return
    const updated = { ...vault, data }
    await persistVault(updated)
    setVault(updated)
  }

  if (vault !== null) {
    return (
      <BrowserRouter>
        <Routes>
          <Route element={<AppLayout />}>
            <Route
              path="/"
              element={<DashboardPage data={vault.data} onDataChange={updateVaultData} />}
            />
            <Route
              path="/connections"
              element={<ConnectionsPage data={vault.data} onDataChange={updateVaultData} />}
            />
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
