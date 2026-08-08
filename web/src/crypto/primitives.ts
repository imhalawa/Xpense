const aesKeyBits = 256;
const aesGcmNonceBytes = 12;

export interface SealedBytes {
  nonce: Uint8Array;
  ciphertext: Uint8Array;
}

const copyBytes = (bytes: Uint8Array): Uint8Array<ArrayBuffer> =>
  new Uint8Array(Array.from(bytes));

export const randomBytes = (length: number): Uint8Array => {
  if (!Number.isSafeInteger(length) || length < 1) {
    throw new Error("The random byte length must be a positive integer");
  }
  return crypto.getRandomValues(new Uint8Array(length));
};

export const generateSymmetricKey = (): Promise<CryptoKey> =>
  crypto.subtle.generateKey({ name: "AES-GCM", length: aesKeyBits }, false, ["encrypt", "decrypt"]);

export const importSymmetricKey = (
  bytes: Uint8Array,
  options: { extractable: boolean },
): Promise<CryptoKey> =>
  crypto.subtle.importKey(
    "raw",
    copyBytes(bytes),
    { name: "AES-GCM", length: aesKeyBits },
    options.extractable,
    ["encrypt", "decrypt"],
  );

export const encryptWithAdditionalData = async (
  key: CryptoKey,
  plaintext: Uint8Array,
  additionalData: Uint8Array,
): Promise<SealedBytes> => {
  const nonce = randomBytes(aesGcmNonceBytes);
  const ciphertext = await crypto.subtle.encrypt(
    {
      name: "AES-GCM",
      iv: copyBytes(nonce),
      additionalData: copyBytes(additionalData),
      tagLength: 128,
    },
    key,
    copyBytes(plaintext),
  );
  return { nonce, ciphertext: new Uint8Array(ciphertext) };
};

export const decryptWithAdditionalData = async (
  key: CryptoKey,
  nonce: Uint8Array,
  ciphertext: Uint8Array,
  additionalData: Uint8Array,
): Promise<Uint8Array> => {
  const plaintext = await crypto.subtle.decrypt(
    {
      name: "AES-GCM",
      iv: copyBytes(nonce),
      additionalData: copyBytes(additionalData),
      tagLength: 128,
    },
    key,
    copyBytes(ciphertext),
  );
  return new Uint8Array(plaintext);
};

export const deriveWrappingKey = async (
  inputKeyMaterial: Uint8Array,
  salt: Uint8Array,
  info: Uint8Array,
  options: { extractable?: boolean } = {},
): Promise<CryptoKey> => {
  const material = await crypto.subtle.importKey(
    "raw",
    copyBytes(inputKeyMaterial),
    "HKDF",
    false,
    ["deriveKey"],
  );
  return crypto.subtle.deriveKey(
    {
      name: "HKDF",
      hash: "SHA-256",
      salt: copyBytes(salt),
      info: copyBytes(info),
    },
    material,
    { name: "AES-GCM", length: aesKeyBits },
    options.extractable ?? false,
    ["encrypt", "decrypt"],
  );
};
