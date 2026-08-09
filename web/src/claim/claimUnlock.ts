import axios from "axios";
import { unwrapMasterKey } from "../crypto/keyHierarchy";
import type { MasterKeyWrapperDescriptor } from "../crypto/protocol";
import {
  requestPrfAssertion,
  serializeAssertionForApi,
  type PrfAssertionResult,
} from "../vault/passkey";

export interface ClaimPasskeyWrapper {
  id: string;
  kind: number;
  credentialId: string | null;
  salt: string;
  ciphertext: string;
  nonce: string;
  protocolVersion: number;
  label: string | null;
}

export interface ClaimIdentity {
  id: string;
  email: string;
  vaultWrappers: ClaimPasskeyWrapper[];
}

export interface ClaimPasskeySelection {
  id: string;
  label: string;
}

interface ClaimPasskeyOptions {
  optionsJson: string;
  pendingPasskeyAssertionId: string;
}

export interface ClaimUnlockDependencies {
  identity(): Promise<ClaimIdentity>;
  options(email: string): Promise<ClaimPasskeyOptions>;
  requestAssertion(
    options: CredentialRequestOptions,
    salt: Uint8Array,
  ): Promise<PrfAssertionResult>;
  signIn(pendingPasskeyAssertionId: string, credentialJson: string): Promise<ClaimIdentity>;
  unwrap(
    wrappingKey: CryptoKey,
    sealed: { nonce: Uint8Array; ciphertext: Uint8Array },
    descriptor: MasterKeyWrapperDescriptor,
  ): Promise<CryptoKey>;
  unlockWorker(masterKey: CryptoKey, userId: string): Promise<boolean>;
}

const antiforgeryPath = "/api/v1/auth/antiforgery";

const decodeBase64 = (value: string, errorMessage: string): Uint8Array<ArrayBuffer> => {
  try {
    const normalized = value.replace(/-/gu, "+").replace(/_/gu, "/");
    const padding = "=".repeat((4 - normalized.length % 4) % 4);
    const binary = atob(`${normalized}${padding}`);
    if (binary.length === 0) throw new Error();
    return new Uint8Array(Array.from(binary, (character) => character.charCodeAt(0)));
  } catch {
    throw new Error(errorMessage);
  }
};

const sameBytes = (left: Uint8Array, right: Uint8Array): boolean =>
  left.length === right.length && left.every((value, index) => value === right[index]);

const requestOptions = (
  optionsJson: string,
  credentialId: Uint8Array,
): CredentialRequestOptions => {
  let parsed: Record<string, unknown>;
  try {
    parsed = JSON.parse(optionsJson) as Record<string, unknown>;
  } catch {
    throw new Error("The passkey request options are invalid.");
  }
  const source = parsed.publicKey !== null && typeof parsed.publicKey === "object"
    ? parsed.publicKey as Record<string, unknown>
    : parsed;
  if (typeof source.challenge !== "string" || typeof source.rpId !== "string" || source.rpId === "") {
    throw new Error("The passkey request options are invalid.");
  }
  return {
    publicKey: {
      ...source,
      challenge: decodeBase64(source.challenge, "The passkey request options are invalid."),
      allowCredentials: [{ type: "public-key", id: credentialId }],
    } as PublicKeyCredentialRequestOptions,
  };
};

const antiforgeryConfig = async (): Promise<{ headers: Record<string, string> }> => ({
  headers: {
    "X-Xpense-Antiforgery": (await axios.get<{ requestToken: string }>(antiforgeryPath)).data.requestToken,
  },
});

const productionDependencies = (
  unlockWorker: ClaimUnlockDependencies["unlockWorker"],
): ClaimUnlockDependencies => ({
  identity: async () => (await axios.get<ClaimIdentity>("/api/v1/auth/me")).data,
  options: async (email) => (await axios.post<ClaimPasskeyOptions>(
    "/api/v1/auth/passkey/options",
    { email },
    await antiforgeryConfig(),
  )).data,
  requestAssertion: requestPrfAssertion,
  signIn: async (pendingPasskeyAssertionId, credentialJson) =>
    (await axios.post<ClaimIdentity>(
      "/api/v1/auth/passkey/sign-in",
      { pendingPasskeyAssertionId, credentialJson },
      await antiforgeryConfig(),
    )).data,
  unwrap: unwrapMasterKey,
  unlockWorker,
});

const usableWrapper = (wrapper: ClaimPasskeyWrapper): boolean =>
  wrapper.kind === 0 && wrapper.credentialId !== null && wrapper.protocolVersion === 1;

export const legacyClaimPasskeys = (identity: ClaimIdentity): ClaimPasskeySelection[] =>
  identity.vaultWrappers.filter(usableWrapper).map((wrapper, index) => ({
    id: wrapper.id,
    label: wrapper.label?.trim() || `Passkey ${index + 1}`,
  }));

export const loadLegacyClaimPasskeys = async (): Promise<ClaimPasskeySelection[]> =>
  legacyClaimPasskeys((await axios.get<ClaimIdentity>("/api/v1/auth/me")).data);

export const unlockLegacyClaimWithPasskey = async (
  dependencies: ClaimUnlockDependencies,
  selectedWrapperId: string,
): Promise<boolean> => {
  const initialIdentity = await dependencies.identity();
  const selected = initialIdentity.vaultWrappers.find((wrapper) =>
    wrapper.id === selectedWrapperId && usableWrapper(wrapper));
  if (selected === undefined || selected.credentialId === null) {
    throw new Error("The selected passkey vault wrapper is unavailable.");
  }
  const credentialId = decodeBase64(selected.credentialId, "The passkey vault wrapper is invalid.");
  const salt = decodeBase64(selected.salt, "The passkey vault wrapper is invalid.");
  const options = await dependencies.options(initialIdentity.email);
  const assertion = await dependencies.requestAssertion(
    requestOptions(options.optionsJson, credentialId),
    salt,
  );
  if (!sameBytes(new Uint8Array(assertion.credential.rawId), credentialId)) {
    throw new Error("The selected passkey wrapper did not match the signed-in credential.");
  }
  const signedInIdentity = await dependencies.signIn(
    options.pendingPasskeyAssertionId,
    JSON.stringify(serializeAssertionForApi(assertion.credential)),
  );
  const signedInWrapper = signedInIdentity.id === initialIdentity.id
    ? signedInIdentity.vaultWrappers.find((wrapper) =>
        wrapper.id === selectedWrapperId &&
        usableWrapper(wrapper) &&
        wrapper.credentialId !== null &&
        sameBytes(
          decodeBase64(wrapper.credentialId, "The passkey vault wrapper is invalid."),
          credentialId,
        ))
    : undefined;
  if (signedInWrapper === undefined) {
    throw new Error("The selected passkey wrapper did not match the signed-in credential.");
  }
  const masterKey = await dependencies.unwrap(
    assertion.wrappingKey,
    {
      nonce: decodeBase64(signedInWrapper.nonce, "The passkey vault wrapper is invalid."),
      ciphertext: decodeBase64(signedInWrapper.ciphertext, "The passkey vault wrapper is invalid."),
    },
    { userId: signedInIdentity.id, wrapperKind: "passkey", wrapperId: signedInWrapper.id },
  );
  return dependencies.unlockWorker(masterKey, signedInIdentity.id);
};

export const unlockLegacyClaim = (
  unlockWorker: ClaimUnlockDependencies["unlockWorker"],
  selectedWrapperId: string,
): Promise<boolean> => unlockLegacyClaimWithPasskey(
  productionDependencies(unlockWorker),
  selectedWrapperId,
);
