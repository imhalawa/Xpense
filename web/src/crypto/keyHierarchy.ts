import {
  generateEncryptionIdentity,
  importEncryptionPrivateKey,
  serializeEncryptionPrivateKey,
  serializeEncryptionPublicKey,
} from "./hpke";
import {
  decryptWithAdditionalData,
  encryptWithAdditionalData,
  exportSymmetricKey,
  generateSymmetricKey,
  importSymmetricKey,
  type SealedBytes,
} from "./primitives";
import {
  encryptionIdentityAdditionalData,
  envelopeAdditionalData,
  masterKeyAdditionalData,
  payloadAdditionalData,
  type EnvelopeDescriptor,
  type MasterKeyWrapperDescriptor,
  type PayloadDescriptor,
} from "./protocol";

const nonceBytes = 12;

export interface EncryptedIdentity {
  publicKeyBytes: Uint8Array;
  encryptedPrivateKey: Uint8Array;
}

const packSealedBytes = (sealed: SealedBytes): Uint8Array => {
  const packed = new Uint8Array(sealed.nonce.length + sealed.ciphertext.length);
  packed.set(sealed.nonce);
  packed.set(sealed.ciphertext, sealed.nonce.length);
  return packed;
};

const unpackSealedBytes = (packed: Uint8Array): SealedBytes => {
  if (packed.length <= nonceBytes) {
    throw new Error("The encrypted value is incomplete");
  }
  return {
    nonce: packed.slice(0, nonceBytes),
    ciphertext: packed.slice(nonceBytes),
  };
};

export const createUserMasterKey = (): Promise<CryptoKey> =>
  generateSymmetricKey({ extractable: true });

export const createRecordKey = (): Promise<CryptoKey> =>
  generateSymmetricKey({ extractable: true });

export const createEncryptionIdentity = async (
  userMasterKey: CryptoKey,
  userId: string,
): Promise<EncryptedIdentity> => {
  const identity = await generateEncryptionIdentity();
  const publicKeyBytes = await serializeEncryptionPublicKey(identity.publicKey);
  const privateKeyBytes = await serializeEncryptionPrivateKey(identity.privateKey);
  const encryptedPrivateKey = await encryptWithAdditionalData(
    userMasterKey,
    privateKeyBytes,
    encryptionIdentityAdditionalData({ userId }),
  );
  privateKeyBytes.fill(0);
  return {
    publicKeyBytes,
    encryptedPrivateKey: packSealedBytes(encryptedPrivateKey),
  };
};

export const unlockEncryptionIdentity = async (
  userMasterKey: CryptoKey,
  userId: string,
  encryptedPrivateKey: Uint8Array,
): Promise<CryptoKey> => {
  const sealed = unpackSealedBytes(encryptedPrivateKey);
  const privateKeyBytes = await decryptWithAdditionalData(
    userMasterKey,
    sealed.nonce,
    sealed.ciphertext,
    encryptionIdentityAdditionalData({ userId }),
  );
  try {
    return await importEncryptionPrivateKey(privateKeyBytes);
  } finally {
    privateKeyBytes.fill(0);
  }
};

export const sealRecordPayload = (
  recordKey: CryptoKey,
  payload: Uint8Array,
  descriptor: PayloadDescriptor,
): Promise<SealedBytes> =>
  encryptWithAdditionalData(recordKey, payload, payloadAdditionalData(descriptor));

export const openRecordPayload = (
  recordKey: CryptoKey,
  sealed: SealedBytes,
  descriptor: PayloadDescriptor,
): Promise<Uint8Array> =>
  decryptWithAdditionalData(
    recordKey,
    sealed.nonce,
    sealed.ciphertext,
    payloadAdditionalData(descriptor),
  );

export const wrapRecordKeyForOwner = async (
  userMasterKey: CryptoKey,
  recordKey: CryptoKey,
  descriptor: EnvelopeDescriptor,
): Promise<SealedBytes> => {
  const recordKeyBytes = await exportSymmetricKey(recordKey);
  try {
    return await encryptWithAdditionalData(
      userMasterKey,
      recordKeyBytes,
      envelopeAdditionalData(descriptor),
    );
  } finally {
    recordKeyBytes.fill(0);
  }
};

export const unwrapRecordKeyForOwner = async (
  userMasterKey: CryptoKey,
  sealed: SealedBytes,
  descriptor: EnvelopeDescriptor,
): Promise<CryptoKey> => {
  const recordKeyBytes = await decryptWithAdditionalData(
    userMasterKey,
    sealed.nonce,
    sealed.ciphertext,
    envelopeAdditionalData(descriptor),
  );
  try {
    return await importSymmetricKey(recordKeyBytes, { extractable: true });
  } finally {
    recordKeyBytes.fill(0);
  }
};

export const wrapMasterKey = async (
  wrappingKey: CryptoKey,
  userMasterKey: CryptoKey,
  descriptor: MasterKeyWrapperDescriptor,
): Promise<SealedBytes> => {
  const masterKeyBytes = await exportSymmetricKey(userMasterKey);
  try {
    return await encryptWithAdditionalData(
      wrappingKey,
      masterKeyBytes,
      masterKeyAdditionalData(descriptor),
    );
  } finally {
    masterKeyBytes.fill(0);
  }
};

export const unwrapMasterKey = async (
  wrappingKey: CryptoKey,
  sealed: SealedBytes,
  descriptor: MasterKeyWrapperDescriptor,
): Promise<CryptoKey> => {
  const masterKeyBytes = await decryptWithAdditionalData(
    wrappingKey,
    sealed.nonce,
    sealed.ciphertext,
    masterKeyAdditionalData(descriptor),
  );
  try {
    return await importSymmetricKey(masterKeyBytes, { extractable: true });
  } finally {
    masterKeyBytes.fill(0);
  }
};
