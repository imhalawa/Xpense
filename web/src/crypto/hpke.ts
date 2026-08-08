import {
  Aes256Gcm,
  CipherSuite,
  DhkemX25519HkdfSha256,
  HkdfSha256,
} from "@hpke/core";

const hpkeContext = new TextEncoder().encode("xpense/v1/group-key-envelope");
const suite = new CipherSuite({
  kem: new DhkemX25519HkdfSha256(),
  kdf: new HkdfSha256(),
  aead: new Aes256Gcm(),
});

export interface HpkeEnvelope {
  encapsulatedKey: Uint8Array;
  ciphertext: Uint8Array;
}

const copyBytes = (bytes: Uint8Array): Uint8Array<ArrayBuffer> =>
  new Uint8Array(Array.from(bytes));

export const generateEncryptionIdentity = (): Promise<CryptoKeyPair> =>
  suite.kem.generateKeyPair();

export const serializeEncryptionPublicKey = async (key: CryptoKey): Promise<Uint8Array> =>
  new Uint8Array(await suite.kem.serializePublicKey(key));

export const serializeEncryptionPrivateKey = async (key: CryptoKey): Promise<Uint8Array> =>
  new Uint8Array(await suite.kem.serializePrivateKey(key));

export const importEncryptionPublicKey = (bytes: Uint8Array): Promise<CryptoKey> =>
  suite.kem.deserializePublicKey(copyBytes(bytes));

export const importEncryptionPrivateKey = async (bytes: Uint8Array): Promise<CryptoKey> => {
  const extractableKey = await suite.kem.deserializePrivateKey(copyBytes(bytes));
  const jsonWebKey = await crypto.subtle.exportKey("jwk", extractableKey);
  return crypto.subtle.importKey("jwk", jsonWebKey, { name: "X25519" }, false, ["deriveBits"]);
};

export const sealToPublicKey = async (
  recipientPublicKey: CryptoKey,
  plaintext: Uint8Array,
  additionalData: Uint8Array,
): Promise<HpkeEnvelope> => {
  const sender = await suite.createSenderContext({
    recipientPublicKey,
    info: copyBytes(hpkeContext),
  });
  const ciphertext = await sender.seal(copyBytes(plaintext), copyBytes(additionalData));
  return {
    encapsulatedKey: new Uint8Array(sender.enc),
    ciphertext: new Uint8Array(ciphertext),
  };
};

export const openWithPrivateKey = async (
  recipientPrivateKey: CryptoKey,
  encapsulatedKey: Uint8Array,
  ciphertext: Uint8Array,
  additionalData: Uint8Array,
): Promise<Uint8Array> => {
  const recipient = await suite.createRecipientContext({
    recipientKey: recipientPrivateKey,
    enc: copyBytes(encapsulatedKey),
    info: copyBytes(hpkeContext),
  });
  const plaintext = await recipient.open(copyBytes(ciphertext), copyBytes(additionalData));
  return new Uint8Array(plaintext);
};
