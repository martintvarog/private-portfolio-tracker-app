import type { ConnectorSyncResult } from './api.ts'
import {decryptJson, deriveKey, encryptJson, randomBytes} from './crypto.ts'
import {loadVaultRecord, saveVaultRecord, type VaultRecord} from './db.ts'

// One entry per credential — Fio issues one token PER ACCOUNT, so two Fio
// accounts = two entries with the same source and different accountLabel.
export type StoredCredential = {
  source: string
  credential: string
  accountLabel?: string
}

export type VaultData = {
  syncResults: ConnectorSyncResult[]
  credentials: StoredCredential[]
}
export type UnlockedVault = { key: CryptoKey; salt: Uint8Array<ArrayBuffer>; data: VaultData }

const saltLength = 16
const vaultVersion = 1;

export async function vaultExists(): Promise<boolean> {
  return !!(await loadVaultRecord());
}

// salt → key → empty data → encrypt → save → return
export async function createVault(passphrase: string): Promise<UnlockedVault> {
  const salt = randomBytes(saltLength)
  const key = await deriveKey(passphrase, salt)

  const emptyData: VaultData = { syncResults: [], credentials: [] }
  const { iv, ciphertext } = await encryptJson(key, emptyData)

  await saveVaultRecord({ id: 'vault', version: vaultVersion, salt, iv, ciphertext })
  return { key, salt, data: emptyData }
}

// TODO (Martin): load record → deriveKey with the STORED salt → decryptJson
// with the stored iv (throws = wrong passphrase — let it propagate, the
// UnlockPage catch handles it). Return { key, salt, data }.
export async function unlockVault(passphrase: string): Promise<UnlockedVault> {
    const vault = await loadExistingVaultRecord();
    const key = await deriveKey(passphrase, vault?.salt);
    const data = await decryptJson<VaultData>(key, vault?.iv, vault?.ciphertext);

    return {key: key, salt: vault?.salt, data: data}
}

// TODO (Martin): encryptJson(vault.key, vault.data) → save record with the
// SAME salt and the freshly returned iv/ciphertext.
export async function persistVault(vault: UnlockedVault): Promise<void> {
    const {iv, ciphertext} = await encryptJson(vault.key, vault.data);
    await saveVaultRecord({id: 'vault',  version: vaultVersion,salt: vault.salt, iv, ciphertext})
}

async function loadExistingVaultRecord(): Promise<VaultRecord>{
    const vault = await loadVaultRecord();
    if (vault === undefined) throw new Error('No vault exists - create one first.')

    return vault;
}
