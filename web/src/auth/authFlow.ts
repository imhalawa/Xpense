import { createEncryptionIdentity, createUserMasterKey, wrapMasterKey } from "../crypto/keyHierarchy";
import { PROTOCOL_VERSION } from "../crypto/protocol";
import {
  createPasskeyWithPrf,
  serializeAssertionForApi,
  serializeAttestationForApi,
  type CreatePasskeyWithPrfOptions,
  type CreatePasskeyWithPrfResult,
} from "../vault/passkey";
import {
  encodeBase64,
  localPrfAssertionOptions,
  parseCredentialOptions,
  registrationUserId,
  toCreationOptions,
  toRequestOptions,
} from "./authCodec";

const invalidOptions = "The passkey options from the server are invalid.";
const nonceBytes = 12;
const saltBytes = 32;

export interface CeremonyOptions {
  optionsJson: string;
}

export interface RegistrationOptions extends CeremonyOptions {
  pendingRegistrationId: string;
}

export interface AssertionOptions extends CeremonyOptions {
  pendingPasskeyAssertionId: string;
}

export interface RegisterRequestBody {
  pendingRegistrationId: string;
  credentialJson: string;
  encryptionPublicKey: string;
  encryptedPrivateKey: string;
  encryptedPrivateKeyNonce: string;
  vaultWrapper: {
    id: string;
    salt: string;
    ciphertext: string;
    nonce: string;
    label: string | null;
  };
  protocolVersion: number;
  invitationToken: string | null;
}

export interface RegisterDependencies {
  registrationOptions(email: string): Promise<RegistrationOptions>;
  createPasskey(options: CreatePasskeyWithPrfOptions): Promise<CreatePasskeyWithPrfResult>;
  register(body: RegisterRequestBody): Promise<{ id: string }>;
  unlockWorker(
    masterKey: CryptoKey,
    userId: string,
    encryptedPrivateKey?: Uint8Array,
  ): Promise<boolean>;
}

export interface SignInDependencies {
  assertionOptions(email: string): Promise<AssertionOptions>;
  requestAssertion(options: CredentialRequestOptions): Promise<PublicKeyCredential>;
  signIn(pendingPasskeyAssertionId: string, credentialJson: string): Promise<{ id: string }>;
}

export interface RegisterInput {
  email: string;
  passkeyLabel: string | null;
  invitationToken: string | null;
}

export const registerWithPasskey = async (
  dependencies: RegisterDependencies,
  input: RegisterInput,
): Promise<{ userId: string }> => {
  const registration = await dependencies.registrationOptions(input.email);
  const options = parseCredentialOptions(registration.optionsJson, invalidOptions);
  const userId = registrationUserId(options, invalidOptions);
  const salt = crypto.getRandomValues(new Uint8Array(saltBytes));

  const passkey = await dependencies.createPasskey({
    creationOptions: toCreationOptions(options, invalidOptions),
    assertionOptions: localPrfAssertionOptions(options),
    salt,
  });

  if (passkey.status === "prfUnsupported") {
    throw new Error(
      "This passkey cannot protect an encrypted vault. Use an authenticator that supports the PRF extension.",
    );
  }

  const masterKey = await createUserMasterKey();
  const identity = await createEncryptionIdentity(masterKey, userId);
  const wrapperId = crypto.randomUUID();
  const sealed = await wrapMasterKey(passkey.wrappingKey, masterKey, {
    userId,
    wrapperKind: "passkey",
    wrapperId,
  });

  await dependencies.register({
    pendingRegistrationId: registration.pendingRegistrationId,
    credentialJson: JSON.stringify(serializeAttestationForApi(passkey.credential)),
    encryptionPublicKey: encodeBase64(identity.publicKeyBytes),
    encryptedPrivateKeyNonce: encodeBase64(identity.encryptedPrivateKey.slice(0, nonceBytes)),
    encryptedPrivateKey: encodeBase64(identity.encryptedPrivateKey.slice(nonceBytes)),
    vaultWrapper: {
      id: wrapperId,
      salt: encodeBase64(salt),
      ciphertext: encodeBase64(sealed.ciphertext),
      nonce: encodeBase64(sealed.nonce),
      label: input.passkeyLabel,
    },
    protocolVersion: PROTOCOL_VERSION,
    invitationToken: input.invitationToken,
  });

  await dependencies.unlockWorker(masterKey, userId, identity.encryptedPrivateKey);
  return { userId };
};

export const signInWithPasskey = async (
  dependencies: SignInDependencies,
  email: string,
): Promise<{ userId: string }> => {
  const assertion = await dependencies.assertionOptions(email);
  const options = toRequestOptions(
    parseCredentialOptions(assertion.optionsJson, invalidOptions),
    invalidOptions,
  );
  const credential = await dependencies.requestAssertion(options);
  const identity = await dependencies.signIn(
    assertion.pendingPasskeyAssertionId,
    JSON.stringify(serializeAssertionForApi(credential)),
  );
  return { userId: identity.id };
};

export const browserCreatePasskey = createPasskeyWithPrf;

export const browserRequestAssertion = async (
  options: CredentialRequestOptions,
): Promise<PublicKeyCredential> => {
  const credential = await navigator.credentials.get(options);
  if (credential === null) throw new Error("The browser did not return a passkey credential.");
  return credential as PublicKeyCredential;
};
