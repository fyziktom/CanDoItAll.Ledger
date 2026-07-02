import { webcrypto } from "node:crypto";

if (!globalThis.crypto) {
  Object.defineProperty(globalThis, "crypto", {
    value: webcrypto,
    configurable: true
  });
}

if (!globalThis.atob) {
  Object.defineProperty(globalThis, "atob", {
    value: (value) => Buffer.from(value, "base64").toString("binary"),
    configurable: true
  });
}

if (!globalThis.btoa) {
  Object.defineProperty(globalThis, "btoa", {
    value: (value) => Buffer.from(value, "binary").toString("base64"),
    configurable: true
  });
}

const cryptoModule = await import("../../../../../src/CanDoItAll.Economy.Cryptography.Browser/wwwroot/candoitall-economy-crypto.js");
const encoder = new TextEncoder();
const decoder = new TextDecoder();

const label = bytesToBase64(encoder.encode("cdia.economy.ecdh.p256.v1"));
const associatedData = bytesToBase64(encoder.encode("webcrypto-ecdh-proof"));
const nonce = await cryptoModule.getRandomBytes(12);
const alice = await cryptoModule.createEcdsaP256KeyPair();
const bob = await cryptoModule.createEcdsaP256KeyPair();

const aliceShared = await cryptoModule.deriveEcdhP256Sha256Key(
  alice.privateKeyMaterialBase64,
  bob.publicKeyMaterialBase64,
  label);
const bobShared = await cryptoModule.deriveEcdhP256Sha256Key(
  bob.privateKeyMaterialBase64,
  alice.publicKeyMaterialBase64,
  label);

if (aliceShared !== bobShared) {
  throw new Error("ECDH shared keys do not match.");
}

const plaintext = "browser ecdh encrypted hello";
const ciphertext = await cryptoModule.aesGcmEncrypt(
  aliceShared,
  nonce,
  bytesToBase64(encoder.encode(plaintext)),
  associatedData);
const decryptedBase64 = await cryptoModule.aesGcmDecrypt(
  bobShared,
  nonce,
  ciphertext,
  associatedData);
const decrypted = decoder.decode(base64ToBytes(decryptedBase64));

if (decrypted !== plaintext) {
  throw new Error("AES-GCM decrypt did not recover plaintext.");
}

let tamperRejected = false;
try {
  const tampered = base64ToBytes(ciphertext);
  tampered[0] ^= 0x01;
  await cryptoModule.aesGcmDecrypt(
    bobShared,
    nonce,
    bytesToBase64(tampered),
    associatedData);
} catch {
  tamperRejected = true;
}

if (!tamperRejected) {
  throw new Error("AES-GCM tamper was not rejected.");
}

console.log(JSON.stringify({
  ecdhSharedKeyBytes: base64ToBytes(aliceShared).byteLength,
  plaintextBytes: encoder.encode(plaintext).byteLength,
  ciphertextWithTagBytes: base64ToBytes(ciphertext).byteLength,
  tamperRejected
}, null, 2));

function bytesToBase64(bytes) {
  return Buffer.from(bytes).toString("base64");
}

function base64ToBytes(base64) {
  return new Uint8Array(Buffer.from(base64, "base64"));
}
