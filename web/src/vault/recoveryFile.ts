import { unwrapMasterKey, wrapMasterKey } from "../crypto/keyHierarchy";
import {
  deriveWrappingKey,
  randomBytes,
} from "../crypto/primitives";
import {
  RECOVERY_FILE_WRAPPING_INFO,
  type MasterKeyWrapperDescriptor,
} from "../crypto/protocol";

const recoveryFileHeader = "XPENSE RECOVERY FILE 1";
const authenticationTokenPrefix = "Authentication token: ";
const vaultSecretPrefix = "Vault secret: ";
const checksumPrefix = "Checksum: ";
const recoverySecretBytes = 32;

const webCryptoBytes = (bytes: Uint8Array): Uint8Array<ArrayBuffer> =>
  new Uint8Array(Array.from(bytes));

export interface RecoveryFileWrapper {
  id: string;
  userId: string;
  kind: "recoveryFile";
  nonce: Uint8Array;
  ciphertext: Uint8Array;
}

export interface RecoveryFileServerPayload {
  authenticationTokenHash: string;
}

export interface RecoveryFileBundle {
  fileText: string;
  serverPayload: RecoveryFileServerPayload;
  wrapper: RecoveryFileWrapper;
}

export interface ParsedRecoveryFile {
  authenticationToken: Uint8Array;
  vaultSecret: Uint8Array;
}

const encodeBase64Url = (bytes: Uint8Array): string => {
  let binary = "";
  for (const value of bytes) binary += String.fromCharCode(value);
  return btoa(binary).replace(/\+/gu, "-").replace(/\//gu, "_").replace(/=+$/u, "");
};

const decodeBase64Url = (encoded: string): Uint8Array => {
  const padding = "=".repeat((4 - (encoded.length % 4)) % 4);
  const binary = atob(encoded.replace(/-/gu, "+").replace(/_/gu, "/") + padding);
  return Uint8Array.from(binary, (character) => character.charCodeAt(0));
};

const concatenate = (...parts: Uint8Array[]): Uint8Array<ArrayBuffer> => {
  const result: Uint8Array<ArrayBuffer> = new Uint8Array(
    parts.reduce((total, part) => total + part.length, 0),
  );
  let offset = 0;
  for (const part of parts) {
    result.set(part, offset);
    offset += part.length;
  }
  return result;
};

const checksum = async (
  authenticationToken: Uint8Array,
  vaultSecret: Uint8Array,
): Promise<Uint8Array> => {
  const input = concatenate(
    new TextEncoder().encode(recoveryFileHeader),
    authenticationToken,
    vaultSecret,
  );
  return new Uint8Array(await crypto.subtle.digest("SHA-256", input));
};

const equalBytes = (first: Uint8Array, second: Uint8Array): boolean => {
  if (first.length !== second.length) return false;
  let difference = 0;
  for (let index = 0; index < first.length; index += 1) {
    difference |= first[index] ^ second[index];
  }
  return difference === 0;
};

const descriptorFromWrapper = (wrapper: RecoveryFileWrapper): MasterKeyWrapperDescriptor => ({
  userId: wrapper.userId,
  wrapperKind: wrapper.kind,
  wrapperId: wrapper.id,
});

export const deriveRecoveryFileWrappingKey = (vaultSecret: Uint8Array): Promise<CryptoKey> =>
  deriveWrappingKey(
    vaultSecret,
    new Uint8Array(),
    new TextEncoder().encode(RECOVERY_FILE_WRAPPING_INFO),
  );

export const parseRecoveryFile = async (fileText: string): Promise<ParsedRecoveryFile> => {
  try {
    const lines = fileText.trim().split(/\r?\n/u);
    if (
      lines.length !== 4 ||
      lines[0] !== recoveryFileHeader ||
      !lines[1].startsWith(authenticationTokenPrefix) ||
      !lines[2].startsWith(vaultSecretPrefix) ||
      !lines[3].startsWith(checksumPrefix)
    ) {
      throw new Error();
    }
    const authenticationToken = decodeBase64Url(
      lines[1].slice(authenticationTokenPrefix.length),
    );
    const vaultSecret = decodeBase64Url(lines[2].slice(vaultSecretPrefix.length));
    const storedChecksum = decodeBase64Url(lines[3].slice(checksumPrefix.length));
    if (
      authenticationToken.length !== recoverySecretBytes ||
      vaultSecret.length !== recoverySecretBytes ||
      !equalBytes(storedChecksum, await checksum(authenticationToken, vaultSecret))
    ) {
      throw new Error();
    }
    return { authenticationToken, vaultSecret };
  } catch {
    throw new Error("The recovery file is corrupted");
  }
};

export const createRecoveryFile = async (
  userMasterKey: CryptoKey,
  descriptor: MasterKeyWrapperDescriptor,
): Promise<RecoveryFileBundle> => {
  if (descriptor.wrapperKind !== "recoveryFile") {
    throw new Error("The master-key wrapper must be a recovery file");
  }
  const authenticationToken = randomBytes(recoverySecretBytes);
  const vaultSecret = randomBytes(recoverySecretBytes);
  try {
    const [tokenHash, fileChecksum, wrappingKey] = await Promise.all([
      crypto.subtle.digest("SHA-256", webCryptoBytes(authenticationToken)),
      checksum(authenticationToken, vaultSecret),
      deriveRecoveryFileWrappingKey(vaultSecret),
    ]);
    const sealed = await wrapMasterKey(wrappingKey, userMasterKey, descriptor);
    return {
      fileText: [
        recoveryFileHeader,
        `${authenticationTokenPrefix}${encodeBase64Url(authenticationToken)}`,
        `${vaultSecretPrefix}${encodeBase64Url(vaultSecret)}`,
        `${checksumPrefix}${encodeBase64Url(fileChecksum)}`,
      ].join("\n"),
      serverPayload: {
        authenticationTokenHash: encodeBase64Url(new Uint8Array(tokenHash)),
      },
      wrapper: {
        id: descriptor.wrapperId,
        userId: descriptor.userId,
        kind: "recoveryFile",
        nonce: sealed.nonce,
        ciphertext: sealed.ciphertext,
      },
    };
  } finally {
    authenticationToken.fill(0);
    vaultSecret.fill(0);
  }
};

export const unwrapRecoveryFile = async (
  wrapper: RecoveryFileWrapper,
  fileText: string,
): Promise<CryptoKey> => {
  const parsed = await parseRecoveryFile(fileText);
  try {
    const wrappingKey = await deriveRecoveryFileWrappingKey(parsed.vaultSecret);
    return await unwrapMasterKey(
      wrappingKey,
      { nonce: wrapper.nonce, ciphertext: wrapper.ciphertext },
      descriptorFromWrapper(wrapper),
    );
  } finally {
    parsed.authenticationToken.fill(0);
    parsed.vaultSecret.fill(0);
  }
};

export const replaceRecoveryFile = (
  userMasterKey: CryptoKey,
  descriptor: MasterKeyWrapperDescriptor,
): Promise<RecoveryFileBundle> => createRecoveryFile(userMasterKey, descriptor);
