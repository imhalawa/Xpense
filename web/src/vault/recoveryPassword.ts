import {
  RECOVERY_PASSWORD_PARAMETERS,
  deriveRecoveryKeyMaterial,
  type Argon2idParameters,
} from "../crypto/argon2";
import { unwrapMasterKey, wrapMasterKey } from "../crypto/keyHierarchy";
import { importSymmetricKey, randomBytes } from "../crypto/primitives";
import type { MasterKeyWrapperDescriptor } from "../crypto/protocol";

export interface RecoveryPasswordWrapper {
  id: string;
  userId: string;
  kind: "recoveryPassword";
  salt: Uint8Array;
  parameters: Argon2idParameters;
  nonce: Uint8Array;
  ciphertext: Uint8Array;
}

export interface RecoveryPasswordOptions {
  parameters?: Argon2idParameters;
}

const descriptorFromWrapper = (
  wrapper: RecoveryPasswordWrapper,
): MasterKeyWrapperDescriptor => ({
  userId: wrapper.userId,
  wrapperKind: wrapper.kind,
  wrapperId: wrapper.id,
});

export const configureRecoveryPassword = async (
  userMasterKey: CryptoKey,
  password: string,
  descriptor: MasterKeyWrapperDescriptor,
  options: RecoveryPasswordOptions = {},
): Promise<RecoveryPasswordWrapper> => {
  if (descriptor.wrapperKind !== "recoveryPassword") {
    throw new Error("The master-key wrapper must be a recovery password");
  }
  const salt = randomBytes(16);
  const parameters = {
    ...(options.parameters ?? RECOVERY_PASSWORD_PARAMETERS),
  };
  const derivedKeyBytes = await deriveRecoveryKeyMaterial(password, salt, parameters);
  try {
    const wrappingKey = await importSymmetricKey(derivedKeyBytes, { extractable: false });
    const sealed = await wrapMasterKey(wrappingKey, userMasterKey, descriptor);
    return {
      id: descriptor.wrapperId,
      userId: descriptor.userId,
      kind: "recoveryPassword",
      salt,
      parameters,
      nonce: sealed.nonce,
      ciphertext: sealed.ciphertext,
    };
  } finally {
    derivedKeyBytes.fill(0);
  }
};

export const unwrapRecoveryPassword = async (
  wrapper: RecoveryPasswordWrapper,
  password: string,
): Promise<CryptoKey> => {
  const derivedKeyBytes = await deriveRecoveryKeyMaterial(
    password,
    wrapper.salt,
    wrapper.parameters,
  );
  try {
    const wrappingKey = await importSymmetricKey(derivedKeyBytes, { extractable: false });
    return await unwrapMasterKey(
      wrappingKey,
      { nonce: wrapper.nonce, ciphertext: wrapper.ciphertext },
      descriptorFromWrapper(wrapper),
    );
  } finally {
    derivedKeyBytes.fill(0);
  }
};
