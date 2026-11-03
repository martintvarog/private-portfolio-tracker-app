const encoder = new TextEncoder()
const decoder = new TextDecoder()

export function randomBytes(length: number): Uint8Array<ArrayBuffer> {
  return crypto.getRandomValues(new Uint8Array(length))
}

export async function deriveKey(passphrase: string, salt: Uint8Array<ArrayBuffer>): Promise<CryptoKey> {
  const material = await crypto.subtle.importKey(
    'raw',
    encoder.encode(passphrase),
    'PBKDF2',
    false,
    ['deriveKey'],
  )
  return crypto.subtle.deriveKey(
    // Iterations aren't stored in the vault record: changing them locks out every existing vault (ADR-0009).
    { name: 'PBKDF2', salt, iterations: 600_000, hash: 'SHA-256' },
    material,
    { name: 'AES-GCM', length: 256 },
    false, // extractable: the key can never be read out
    ['encrypt', 'decrypt'],
  )
}

export async function encryptJson(
  key: CryptoKey,
  data: unknown,
): Promise<{ iv: Uint8Array<ArrayBuffer>; ciphertext: ArrayBuffer }> {
  const iv = randomBytes(12) // fresh per encryption: reusing an IV with the same key breaks GCM
  const ciphertext = await crypto.subtle.encrypt(
    { name: 'AES-GCM', iv },
    key,
    encoder.encode(JSON.stringify(data)),
  )
  return { iv, ciphertext }
}

export async function decryptJson<T>(
  key: CryptoKey,
  iv: Uint8Array<ArrayBuffer>,
  ciphertext: ArrayBuffer,
): Promise<T> {
  const plaintext = await crypto.subtle.decrypt({ name: 'AES-GCM', iv }, key, ciphertext)
  return JSON.parse(decoder.decode(plaintext)) as T
}


