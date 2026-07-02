const databaseName = "CanDoItAll.Economy.AccountVault";
const databaseVersion = 1;
const accountStoreName = "accounts";

export function getRandomBytes(length) {
  ensureWebCrypto();
  if (!Number.isInteger(length) || length <= 0) {
    throw new Error("Random byte length must be positive.");
  }

  const bytes = new Uint8Array(length);
  globalThis.crypto.getRandomValues(bytes);
  return bytesToBase64(bytes);
}

export async function sha256(dataBase64) {
  ensureWebCrypto();
  const hash = await globalThis.crypto.subtle.digest("SHA-256", base64ToBytes(dataBase64));
  return bytesToBase64(new Uint8Array(hash));
}

export async function pbkdf2Sha256(passwordBase64, saltBase64, iterations, byteCount) {
  ensureWebCrypto();
  if (!Number.isInteger(iterations) || iterations <= 0) {
    throw new Error("PBKDF2 iterations must be positive.");
  }

  if (!Number.isInteger(byteCount) || byteCount <= 0) {
    throw new Error("Derived key byte count must be positive.");
  }

  const key = await globalThis.crypto.subtle.importKey(
    "raw",
    base64ToBytes(passwordBase64),
    "PBKDF2",
    false,
    ["deriveBits"]);
  const bits = await globalThis.crypto.subtle.deriveBits(
    {
      name: "PBKDF2",
      salt: base64ToBytes(saltBase64),
      iterations,
      hash: "SHA-256"
    },
    key,
    byteCount * 8);
  return bytesToBase64(new Uint8Array(bits));
}

export async function aesGcmEncrypt(keyBase64, nonceBase64, plaintextBase64, associatedDataBase64) {
  ensureWebCrypto();
  const key = await importAesKey(keyBase64, ["encrypt"]);
  const encrypted = await globalThis.crypto.subtle.encrypt(
    {
      name: "AES-GCM",
      iv: base64ToBytes(nonceBase64),
      additionalData: base64ToBytes(associatedDataBase64),
      tagLength: 128
    },
    key,
    base64ToBytes(plaintextBase64));
  return bytesToBase64(new Uint8Array(encrypted));
}

export async function aesGcmDecrypt(keyBase64, nonceBase64, ciphertextWithTagBase64, associatedDataBase64) {
  ensureWebCrypto();
  const key = await importAesKey(keyBase64, ["decrypt"]);
  const decrypted = await globalThis.crypto.subtle.decrypt(
    {
      name: "AES-GCM",
      iv: base64ToBytes(nonceBase64),
      additionalData: base64ToBytes(associatedDataBase64),
      tagLength: 128
    },
    key,
    base64ToBytes(ciphertextWithTagBase64));
  return bytesToBase64(new Uint8Array(decrypted));
}

export async function deriveEcdhP256Sha256Key(privateKeyMaterialBase64, otherPublicKeyMaterialBase64, secretAppendBase64) {
  ensureWebCrypto();
  const privateKey = await globalThis.crypto.subtle.importKey(
    "pkcs8",
    base64ToBytes(privateKeyMaterialBase64),
    {
      name: "ECDH",
      namedCurve: "P-256"
    },
    false,
    ["deriveBits"]);
  const publicKey = await globalThis.crypto.subtle.importKey(
    "spki",
    base64ToBytes(otherPublicKeyMaterialBase64),
    {
      name: "ECDH",
      namedCurve: "P-256"
    },
    false,
    []);
  const sharedSecret = new Uint8Array(await globalThis.crypto.subtle.deriveBits(
    {
      name: "ECDH",
      public: publicKey
    },
    privateKey,
    256));
  const secretAppend = base64ToBytes(secretAppendBase64);
  const keyMaterial = new Uint8Array(sharedSecret.length + secretAppend.length);
  keyMaterial.set(sharedSecret, 0);
  keyMaterial.set(secretAppend, sharedSecret.length);
  const keyHash = await globalThis.crypto.subtle.digest("SHA-256", keyMaterial);
  return bytesToBase64(new Uint8Array(keyHash));
}

export async function createEcdsaP256KeyPair() {
  ensureWebCrypto();
  const keyPair = await globalThis.crypto.subtle.generateKey(
    {
      name: "ECDSA",
      namedCurve: "P-256"
    },
    true,
    ["sign", "verify"]);
  const publicKey = await globalThis.crypto.subtle.exportKey("spki", keyPair.publicKey);
  const privateKey = await globalThis.crypto.subtle.exportKey("pkcs8", keyPair.privateKey);
  return {
    publicKeyMaterialBase64: bytesToBase64(new Uint8Array(publicKey)),
    privateKeyMaterialBase64: bytesToBase64(new Uint8Array(privateKey))
  };
}

export async function signEcdsaP256Sha256(payloadBase64, privateKeyMaterialBase64) {
  ensureWebCrypto();
  const key = await globalThis.crypto.subtle.importKey(
    "pkcs8",
    base64ToBytes(privateKeyMaterialBase64),
    {
      name: "ECDSA",
      namedCurve: "P-256"
    },
    false,
    ["sign"]);
  const signature = await globalThis.crypto.subtle.sign(
    {
      name: "ECDSA",
      hash: "SHA-256"
    },
    key,
    base64ToBytes(payloadBase64));
  return bytesToBase64(new Uint8Array(signature));
}

export async function verifyEcdsaP256Sha256(payloadBase64, signatureBase64, publicKeyMaterialBase64) {
  ensureWebCrypto();
  const key = await globalThis.crypto.subtle.importKey(
    "spki",
    base64ToBytes(publicKeyMaterialBase64),
    {
      name: "ECDSA",
      namedCurve: "P-256"
    },
    false,
    ["verify"]);
  return await globalThis.crypto.subtle.verify(
    {
      name: "ECDSA",
      hash: "SHA-256"
    },
    key,
    base64ToBytes(signatureBase64),
    base64ToBytes(payloadBase64));
}

export async function saveVaultRecord(record) {
  ensureIndexedDb();
  if (!record?.vaultId) {
    throw new Error("Vault record id is required.");
  }

  const database = await openDatabase();
  await runStoreRequest(database, "readwrite", (store) => store.put(record));
}

export async function getVaultRecord(vaultId) {
  ensureIndexedDb();
  if (!vaultId) {
    throw new Error("Vault id is required.");
  }

  const database = await openDatabase();
  const result = await runStoreRequest(database, "readonly", (store) => store.get(vaultId));
  return result ?? null;
}

export async function deleteVaultRecord(vaultId) {
  ensureIndexedDb();
  if (!vaultId) {
    throw new Error("Vault id is required.");
  }

  const database = await openDatabase();
  await runStoreRequest(database, "readwrite", (store) => store.delete(vaultId));
}

async function importAesKey(keyBase64, usages) {
  const keyBytes = base64ToBytes(keyBase64);
  if (keyBytes.byteLength !== 32) {
    throw new Error("AES-GCM key must be 32 bytes.");
  }

  return await globalThis.crypto.subtle.importKey(
    "raw",
    keyBytes,
    "AES-GCM",
    false,
    usages);
}

function openDatabase() {
  return new Promise((resolve, reject) => {
    const request = globalThis.indexedDB.open(databaseName, databaseVersion);

    request.onerror = () => reject(request.error);
    request.onsuccess = () => resolve(request.result);
    request.onupgradeneeded = () => {
      const database = request.result;
      if (!database.objectStoreNames.contains(accountStoreName)) {
        database.createObjectStore(accountStoreName, { keyPath: "vaultId" });
      }
    };
  });
}

function runStoreRequest(database, mode, requestFactory) {
  return new Promise((resolve, reject) => {
    const transaction = database.transaction(accountStoreName, mode);
    const store = transaction.objectStore(accountStoreName);
    const request = requestFactory(store);

    request.onerror = () => reject(request.error);
    request.onsuccess = () => resolve(request.result);
    transaction.oncomplete = () => database.close();
    transaction.onabort = () => {
      database.close();
      reject(transaction.error);
    };
  });
}

function base64ToBytes(base64) {
  if (!base64) {
    return new Uint8Array();
  }

  const binary = globalThis.atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index += 1) {
    bytes[index] = binary.charCodeAt(index);
  }

  return bytes;
}

function bytesToBase64(bytes) {
  if (!bytes || bytes.byteLength === 0) {
    return "";
  }

  let binary = "";
  for (let index = 0; index < bytes.length; index += 0x8000) {
    binary += String.fromCharCode(...bytes.subarray(index, index + 0x8000));
  }

  return globalThis.btoa(binary);
}

function ensureWebCrypto() {
  if (!globalThis.crypto?.subtle) {
    throw new Error("Browser WebCrypto API is not available.");
  }
}

function ensureIndexedDb() {
  if (!globalThis.indexedDB) {
    throw new Error("Browser IndexedDB API is not available.");
  }
}
