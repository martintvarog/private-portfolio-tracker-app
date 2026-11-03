import { openDB, type DBSchema, type IDBPDatabase } from 'idb'

export type VaultRecord = {
  id: 'vault'
  version: 1
  salt: Uint8Array<ArrayBuffer>
  iv: Uint8Array<ArrayBuffer>
  ciphertext: ArrayBuffer
}

interface AppDb extends DBSchema {
  vault: { key: string; value: VaultRecord }
}

let dbPromise: Promise<IDBPDatabase<AppDb>> | undefined

function getDb() {
  dbPromise ??= openDB<AppDb>('portfolio-tracker', 1, {
    upgrade(db) {
      db.createObjectStore('vault', { keyPath: 'id' })
    },
  })
  return dbPromise
}

export async function loadVaultRecord(): Promise<VaultRecord | undefined> {
  return (await getDb()).get('vault', 'vault')
}

export async function saveVaultRecord(record: VaultRecord): Promise<void> {
  await (await getDb()).put('vault', record)
}
