// IndexedDB access via `idb` (thin promise wrapper over the real API).
//
// One object store, one record: the sealed vault blob. Everything readable
// lives INSIDE the ciphertext — the record's shape deliberately reveals
// nothing about what the user owns.

import { openDB, type DBSchema, type IDBPDatabase } from 'idb'

export type VaultRecord = {
  id: 'vault'
  version: 1 // vault BLOB format version (what's inside the JSON), for future migrations
  salt: Uint8Array<ArrayBuffer>
  iv: Uint8Array<ArrayBuffer>
  ciphertext: ArrayBuffer
}

interface AppDb extends DBSchema {
  vault: { key: string; value: VaultRecord }
}

let dbPromise: Promise<IDBPDatabase<AppDb>> | undefined

function getDb() {
  // openDB's version + upgrade callback is IndexedDB's migration mechanism:
  // bumping the version re-runs upgrade, where new object stores are created
  // (that's the backlog rule "schema versioned from day one" in practice).
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
