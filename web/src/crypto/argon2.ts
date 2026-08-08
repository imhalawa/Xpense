import { argon2idAsync } from "@noble/hashes/argon2.js";

const minimumRecoveryPasswordCharacters = 14;
const recoverySaltBytes = 16;

export interface Argon2idParameters {
  readonly memoryKibibytes: number;
  readonly iterations: number;
  readonly lanes: number;
  readonly outputBytes: number;
}

export interface Argon2idVectorInputs {
  readonly secret?: Uint8Array;
  readonly associatedData?: Uint8Array;
}

export const RECOVERY_PASSWORD_PARAMETERS: Readonly<Argon2idParameters> = Object.freeze({
  memoryKibibytes: 65_536,
  iterations: 3,
  lanes: 4,
  outputBytes: 32,
});

export const deriveArgon2idKeyMaterial = async (
  password: Uint8Array,
  salt: Uint8Array,
  parameters: Argon2idParameters,
  vectorInputs: Argon2idVectorInputs = {},
): Promise<Uint8Array> => {
  const result = await argon2idAsync(password, salt, {
    m: parameters.memoryKibibytes,
    t: parameters.iterations,
    p: parameters.lanes,
    dkLen: parameters.outputBytes,
    key: vectorInputs.secret,
    personalization: vectorInputs.associatedData,
  });
  return new Uint8Array(Array.from(result));
};

export const deriveRecoveryKeyMaterial = async (
  password: string,
  salt: Uint8Array,
  parameters: Argon2idParameters,
): Promise<Uint8Array> => {
  if (Array.from(password).length < minimumRecoveryPasswordCharacters) {
    throw new Error("The recovery password must contain at least 14 characters");
  }
  if (salt.length !== recoverySaltBytes) {
    throw new Error("The recovery password salt must contain exactly 16 bytes");
  }
  return deriveArgon2idKeyMaterial(new TextEncoder().encode(password), salt, parameters);
};
