import { describe, expect, it, vi } from "vitest";
import { unlockEncryptionIdentity, unwrapMasterKey } from "../crypto/keyHierarchy";
import { derivePasskeyWrappingKey } from "../vault/passkey";
import { decodeBase64, encodeBase64 } from "./authCodec";
import {
  registerWithPasskey,
  signInWithPasskey,
  type RegisterDependencies,
  type RegisterRequestBody,
  type SignInDependencies,
} from "./authFlow";

const userId = "0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b";

const base64Url = (value: string): string =>
  btoa(value).replace(/\+/gu, "-").replace(/\//gu, "_").replace(/=+$/u, "");

const creationOptionsJson = JSON.stringify({
  challenge: base64Url("registration-challenge"),
  rp: { id: "localhost", name: "Xpense" },
  user: { id: base64Url(userId), name: "person@example.test", displayName: "person@example.test" },
  pubKeyCredParams: [{ type: "public-key", alg: -7 }],
});

const requestOptionsJson = JSON.stringify({
  challenge: base64Url("assertion-challenge"),
  rpId: "localhost",
  allowCredentials: [{ type: "public-key", id: base64Url("credential-id") }],
});

const bytes = (value: string): Uint8Array => new TextEncoder().encode(value);

const extensionResults = () => ({ prf: { results: { first: bytes("prf-secret") } } });

const fakeAttestation = () => ({
  id: "credential-id",
  rawId: bytes("credential-id"),
  type: "public-key",
  getClientExtensionResults: extensionResults,
  response: {
    clientDataJSON: bytes("{}"),
    attestationObject: bytes("attestation"),
    getTransports: () => ["internal"],
  },
}) as unknown as PublicKeyCredential;

const fakeAssertion = () => ({
  id: "credential-id",
  rawId: bytes("credential-id"),
  type: "public-key",
  getClientExtensionResults: extensionResults,
  response: {
    clientDataJSON: bytes("{}"),
    authenticatorData: bytes("authenticator"),
    signature: bytes("signature"),
    userHandle: null,
  },
}) as unknown as PublicKeyCredential;

const rawKey = async (key: CryptoKey): Promise<string> =>
  encodeBase64(new Uint8Array(await crypto.subtle.exportKey("raw", key)));

const registerOnce = async (): Promise<{
  body: RegisterRequestBody;
  wrappingKey: CryptoKey;
  unlockedWith: { masterKey: CryptoKey; encryptedPrivateKey?: Uint8Array };
}> => {
  const wrappingKey = await derivePasskeyWrappingKey(bytes("a-stable-prf-output-value"));
  let body: RegisterRequestBody | null = null;
  let unlockedWith: { masterKey: CryptoKey; encryptedPrivateKey?: Uint8Array } | null = null;

  const dependencies: RegisterDependencies = {
    registrationOptions: vi.fn().mockResolvedValue({
      optionsJson: creationOptionsJson,
      pendingRegistrationId: "11111111-1111-4111-8111-111111111111",
    }),
    createPasskey: vi.fn().mockResolvedValue({
      status: "ready",
      credential: fakeAttestation(),
      wrappingKey,
    }),
    register: vi.fn().mockImplementation((sent: RegisterRequestBody) => {
      body = sent;
      return Promise.resolve({ id: userId });
    }),
    unlockWorker: vi.fn().mockImplementation((masterKey: CryptoKey, _id: string, encryptedPrivateKey?: Uint8Array) => {
      unlockedWith = { masterKey, encryptedPrivateKey };
      return Promise.resolve(true);
    }),
  };

  const result = await registerWithPasskey(dependencies, {
    email: "person@example.test",
    passkeyLabel: "Laptop",
    invitationToken: null,
  });

  expect(result.userId).toBe(userId);
  expect(body).not.toBeNull();
  expect(unlockedWith).not.toBeNull();
  return { body: body!, wrappingKey, unlockedWith: unlockedWith! };
};

describe("registerWithPasskey", () => {
  it("submits a wrapper the same identifier can unwrap", async () => {
    const { body, wrappingKey, unlockedWith } = await registerOnce();

    const reopened = await unwrapMasterKey(
      wrappingKey,
      {
        nonce: decodeBase64(body.vaultWrapper.nonce, "nonce"),
        ciphertext: decodeBase64(body.vaultWrapper.ciphertext, "ciphertext"),
      },
      { userId, wrapperKind: "passkey", wrapperId: body.vaultWrapper.id },
    );

    expect(await rawKey(reopened)).toBe(await rawKey(unlockedWith.masterKey));
  });

  it("refuses any other wrapper identifier", async () => {
    const { body, wrappingKey } = await registerOnce();

    await expect(unwrapMasterKey(
      wrappingKey,
      {
        nonce: decodeBase64(body.vaultWrapper.nonce, "nonce"),
        ciphertext: decodeBase64(body.vaultWrapper.ciphertext, "ciphertext"),
      },
      { userId, wrapperKind: "passkey", wrapperId: crypto.randomUUID() },
    )).rejects.toThrow();
  });

  it("splits the encryption identity into the nonce and ciphertext the API expects", async () => {
    const { body, unlockedWith } = await registerOnce();

    const repacked = new Uint8Array([
      ...decodeBase64(body.encryptedPrivateKeyNonce, "nonce"),
      ...decodeBase64(body.encryptedPrivateKey, "ciphertext"),
    ]);

    expect(encodeBase64(repacked)).toBe(encodeBase64(unlockedWith.encryptedPrivateKey!));
    await expect(unlockEncryptionIdentity(unlockedWith.masterKey, userId, repacked)).resolves.toBeDefined();
  });

  it("stops when the authenticator cannot produce a PRF output", async () => {
    const dependencies = {
      registrationOptions: vi.fn().mockResolvedValue({
        optionsJson: creationOptionsJson,
        pendingRegistrationId: "11111111-1111-4111-8111-111111111111",
      }),
      createPasskey: vi.fn().mockResolvedValue({
        status: "prfUnsupported",
        credential: fakeAttestation(),
      }),
      register: vi.fn(),
      unlockWorker: vi.fn(),
    } satisfies RegisterDependencies;

    await expect(registerWithPasskey(dependencies, {
      email: "person@example.test",
      passkeyLabel: null,
      invitationToken: null,
    })).rejects.toThrow(/PRF extension/u);
    expect(dependencies.register).not.toHaveBeenCalled();
  });
});

describe("signInWithPasskey", () => {
  it("returns the signed-in identity without touching the vault", async () => {
    const signIn = vi.fn().mockResolvedValue({ id: userId });
    const dependencies: SignInDependencies = {
      assertionOptions: vi.fn().mockResolvedValue({
        optionsJson: requestOptionsJson,
        pendingPasskeyAssertionId: "22222222-2222-4222-8222-222222222222",
      }),
      requestAssertion: vi.fn().mockResolvedValue(fakeAssertion()),
      signIn,
    };

    const result = await signInWithPasskey(dependencies, "person@example.test");

    expect(result.userId).toBe(userId);
    expect(signIn).toHaveBeenCalledWith(
      "22222222-2222-4222-8222-222222222222",
      expect.stringContaining("credential-id"),
    );
    expect(signIn.mock.calls[0]![1]).not.toContain("prf");
  });
});
