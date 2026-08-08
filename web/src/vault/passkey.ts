import { deriveWrappingKey } from "../crypto/primitives";
import { PASSKEY_WRAPPING_INFO } from "../crypto/protocol";

interface PrfExtensionResult {
  enabled?: boolean;
  results?: { first?: BufferSource };
}

interface PasskeyExtensionResults {
  prf?: PrfExtensionResult;
}

export interface CreatePasskeyWithPrfOptions {
  creationOptions: CredentialCreationOptions;
  assertionOptions: CredentialRequestOptions;
  salt: Uint8Array;
}

export type CreatePasskeyWithPrfResult =
  | {
      status: "ready";
      credential: PublicKeyCredential;
      assertionCredential?: PublicKeyCredential;
      wrappingKey: CryptoKey;
    }
  | { status: "prfUnsupported"; credential: PublicKeyCredential };

export interface PrfAssertionResult {
  credential: PublicKeyCredential;
  wrappingKey: CryptoKey;
}

export interface SerializedAttestation {
  id: string;
  rawId: string;
  type: string;
  response: {
    clientDataJSON: string;
    attestationObject: string;
    transports: string[];
  };
}

export interface SerializedAssertion {
  id: string;
  rawId: string;
  type: string;
  response: {
    clientDataJSON: string;
    authenticatorData: string;
    signature: string;
    userHandle: string | null;
  };
}

const copyBytes = (source: BufferSource): Uint8Array<ArrayBuffer> => {
  if (ArrayBuffer.isView(source)) {
    return new Uint8Array(
      Array.from(new Uint8Array(source.buffer, source.byteOffset, source.byteLength)),
    );
  }
  return new Uint8Array(Array.from(new Uint8Array(source)));
};

const encodeBase64Url = (source: BufferSource): string => {
  let binary = "";
  for (const value of copyBytes(source)) binary += String.fromCharCode(value);
  return btoa(binary).replaceAll("+", "-").replaceAll("/", "_").replace(/=+$/u, "");
};

const requireCredential = (credential: Credential | null): PublicKeyCredential => {
  if (
    credential === null ||
    typeof (credential as PublicKeyCredential).getClientExtensionResults !== "function"
  ) {
    throw new Error("The browser did not return a passkey credential");
  }
  return credential as PublicKeyCredential;
};

const prfResult = (credential: PublicKeyCredential): PrfExtensionResult | undefined =>
  (credential.getClientExtensionResults() as unknown as PasskeyExtensionResults).prf;

const optionsWithPrf = <T extends CredentialCreationOptions | CredentialRequestOptions>(
  options: T,
  salt: Uint8Array,
): T => {
  if (options.publicKey === undefined) {
    throw new Error("The passkey request must contain public-key options");
  }
  return {
    ...options,
    publicKey: {
      ...options.publicKey,
      extensions: {
        ...options.publicKey.extensions,
        prf: { eval: { first: copyBytes(salt) } },
      },
    },
  } as T;
};

const assertionChallenge = (credential: PublicKeyCredential): string => {
  const response = credential.response as AuthenticatorAssertionResponse;
  const decoded = new TextDecoder().decode(response.clientDataJSON);
  const parsed = JSON.parse(decoded) as { challenge?: unknown };
  if (typeof parsed.challenge !== "string") {
    throw new Error("The passkey assertion did not contain a challenge");
  }
  return parsed.challenge;
};

export const derivePasskeyWrappingKey = (prfOutput: Uint8Array): Promise<CryptoKey> =>
  deriveWrappingKey(
    prfOutput,
    new Uint8Array(),
    new TextEncoder().encode(PASSKEY_WRAPPING_INFO),
  );

export const requestPrfAssertion = async (
  options: CredentialRequestOptions,
  salt: Uint8Array,
): Promise<PrfAssertionResult> => {
  const credential = requireCredential(
    await navigator.credentials.get(optionsWithPrf(options, salt)),
  );
  const expectedChallenge = options.publicKey?.challenge;
  if (
    expectedChallenge === undefined ||
    assertionChallenge(credential) !== encodeBase64Url(expectedChallenge)
  ) {
    throw new Error("The passkey assertion challenge does not match");
  }
  const output = prfResult(credential)?.results?.first;
  if (output === undefined) {
    throw new Error("The passkey assertion did not return a PRF result");
  }
  const outputBytes = copyBytes(output);
  try {
    return { credential, wrappingKey: await derivePasskeyWrappingKey(outputBytes) };
  } finally {
    outputBytes.fill(0);
  }
};

export const createPasskeyWithPrf = async (
  options: CreatePasskeyWithPrfOptions,
): Promise<CreatePasskeyWithPrfResult> => {
  const credential = requireCredential(
    await navigator.credentials.create(optionsWithPrf(options.creationOptions, options.salt)),
  );
  const creationPrf = prfResult(credential);
  if (creationPrf?.enabled !== true) {
    return { status: "prfUnsupported", credential };
  }
  const immediateOutput = creationPrf.results?.first;
  if (immediateOutput !== undefined) {
    const outputBytes = copyBytes(immediateOutput);
    try {
      return {
        status: "ready",
        credential,
        wrappingKey: await derivePasskeyWrappingKey(outputBytes),
      };
    } finally {
      outputBytes.fill(0);
    }
  }
  const assertion = await requestPrfAssertion(options.assertionOptions, options.salt);
  return {
    status: "ready",
    credential,
    assertionCredential: assertion.credential,
    wrappingKey: assertion.wrappingKey,
  };
};

export const serializeAttestationForApi = (
  credential: PublicKeyCredential,
): SerializedAttestation => {
  const response = credential.response as AuthenticatorAttestationResponse;
  if (!("attestationObject" in response)) {
    throw new Error("The credential does not contain an attestation response");
  }
  return {
    id: credential.id,
    rawId: encodeBase64Url(credential.rawId),
    type: credential.type,
    response: {
      clientDataJSON: encodeBase64Url(response.clientDataJSON),
      attestationObject: encodeBase64Url(response.attestationObject),
      transports: response.getTransports?.() ?? [],
    },
  };
};

export const serializeAssertionForApi = (
  credential: PublicKeyCredential,
): SerializedAssertion => {
  const response = credential.response as AuthenticatorAssertionResponse;
  if (!("authenticatorData" in response)) {
    throw new Error("The credential does not contain an assertion response");
  }
  return {
    id: credential.id,
    rawId: encodeBase64Url(credential.rawId),
    type: credential.type,
    response: {
      clientDataJSON: encodeBase64Url(response.clientDataJSON),
      authenticatorData: encodeBase64Url(response.authenticatorData),
      signature: encodeBase64Url(response.signature),
      userHandle: response.userHandle === null ? null : encodeBase64Url(response.userHandle),
    },
  };
};
