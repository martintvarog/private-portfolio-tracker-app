import type { ConnectorSyncResult } from './api.ts'
import {decryptJson, deriveKey, encryptJson, randomBytes} from './crypto.ts'
import {loadVaultRecord, saveVaultRecord, type VaultRecord} from './db.ts'

export type Connection = {
  source: string
  credential: string
  accountLabel?: string
}

export type VaultData = {
  syncResults: ConnectorSyncResult[]
  connections: Connection[]
}
export type UnlockedVault = { key: CryptoKey; salt: Uint8Array<ArrayBuffer>; data: VaultData }

const saltLength = 16
const vaultVersion = 1;

export async function vaultExists(): Promise<boolean> {
  return !!(await loadVaultRecord());
}

export async function createVault(passphrase: string): Promise<UnlockedVault> {
  const salt = randomBytes(saltLength)
  const key = await deriveKey(passphrase, salt)

  const emptyData: VaultData = { syncResults: [], connections: [] }
  const { iv, ciphertext } = await encryptJson(key, emptyData)

  await saveVaultRecord({ id: 'vault', version: vaultVersion, salt, iv, ciphertext })
  return { key, salt, data: emptyData }
}

export async function unlockVault(passphrase: string): Promise<UnlockedVault> {
    const vault = await loadExistingVaultRecord();
    const key = await deriveKey(passphrase, vault?.salt);
    const data = await decryptJson<VaultData>(key, vault?.iv, vault?.ciphertext);

    return {key: key, salt: vault?.salt, data: data}
}

export async function persistVault(vault: UnlockedVault): Promise<void> {
    // Keep the original salt: a new one derives a different key and the vault never opens again.
    const {iv, ciphertext} = await encryptJson(vault.key, vault.data);
    await saveVaultRecord({id: 'vault',  version: vaultVersion,salt: vault.salt, iv, ciphertext})
}

async function loadExistingVaultRecord(): Promise<VaultRecord>{
    const vault = await loadVaultRecord();
    if (vault === undefined) throw new Error('No vault exists - create one first.')

    return vault;
}
