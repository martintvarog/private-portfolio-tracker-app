// Vault crypto primitives (WebCrypto, built into every browser).
//
// Passphrase → PBKDF2 (slow, salted) → AES-256-GCM key → one sealed blob.
// PBKDF2 with 600k iterations is the built-in baseline (OWASP 2023 figure);
// upgrading to Argon2id (memory-hard, needs a WASM lib) is an M3 ADR.

const encoder = new TextEncoder()
const decoder = new TextDecoder()

export function randomBytes(length: number): Uint8Array<ArrayBuffer> {
  return crypto.getRandomValues(new Uint8Array(length))
}

// The salt makes identical passphrases produce different keys — it is public,
// stored next to the ciphertext, and must be REUSED on every save (a new salt
// would derive a different key that can't decrypt the old blob).
export async function deriveKey(passphrase: string, salt: Uint8Array<ArrayBuffer>): Promise<CryptoKey> {
  const material = await crypto.subtle.importKey(
    'raw',
    encoder.encode(passphrase),
    'PBKDF2',
    false,
    ['deriveKey'],
  )
  return crypto.subtle.deriveKey(
    { name: 'PBKDF2', salt, iterations: 600_000, hash: 'SHA-256' },
    material,
    { name: 'AES-GCM', length: 256 },
    false, // not extractable: the key can be used but never read out of the CryptoKey object
    ['encrypt', 'decrypt'],
  )
}

// A fresh IV per encryption is mandatory for GCM (reuse breaks it); like the
// salt, it is public and stored alongside the ciphertext.
export async function encryptJson(
  key: CryptoKey,
  data: unknown,
): Promise<{ iv: Uint8Array<ArrayBuffer>; ciphertext: ArrayBuffer }> {
  const iv = randomBytes(12)
  const ciphertext = await crypto.subtle.encrypt(
    { name: 'AES-GCM', iv },
    key,
    encoder.encode(JSON.stringify(data)),
  )
  return { iv, ciphertext }
}

// GCM is authenticated: decrypt VERIFIES the integrity tag first and throws
// on any mismatch — which is exactly what a wrong passphrase produces.
// Callers turn that exception into "wrong passphrase".
export async function decryptJson<T>(
  key: CryptoKey,
  iv: Uint8Array<ArrayBuffer>,
  ciphertext: ArrayBuffer,
): Promise<T> {
  const plaintext = await crypto.subtle.decrypt({ name: 'AES-GCM', iv }, key, ciphertext)
  return JSON.parse(decoder.decode(plaintext)) as T
}


