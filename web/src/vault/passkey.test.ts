import { afterEach, describe, expect, it, vi } from "vitest";
import { decryptWithAdditionalData, encryptWithAdditionalData } from "../crypto/primitives";
import {
  createPasskeyWithPrf,
  requestPrfAssertion,
  serializeAssertionForApi,
  serializeAttestationForApi,
} from "./passkey";

const creationChallenge = new Uint8Array([1, 2, 3]);
const assertionChallenge = new Uint8Array([4, 5, 6]);
const salt = new Uint8Array(32).fill(7);

const creationOptions: CredentialCreationOptions = {
  publicKey: {
    challenge: creationChallenge,
    rp: { name: "Xpense" },
    user: {
      id: new Uint8Array([8]),
      name: "owner@example.test",
      displayName: "Owner",
    },
    pubKeyCredParams: [{ type: "public-key", alg: -7 }],
  },
};

const assertionOptions: CredentialRequestOptions = {
  publicKey: { challenge: assertionChallenge },
};

const base64Url = (bytes: Uint8Array): string => {
  let binary = "";
  for (const value of bytes) binary += String.fromCharCode(value);
  return btoa(binary).replace(/\+/gu, "-").replace(/\//gu, "_").replace(/=+$/u, "");
};

const clientData = (challenge: Uint8Array): ArrayBuffer =>
  new TextEncoder().encode(JSON.stringify({ challenge: base64Url(challenge) })).buffer;

const createCredential = (
  prf: Record<string, unknown>,
  prfBytes = new Uint8Array(32).fill(9),
): PublicKeyCredential =>
  ({
    id: "credential-id",
    rawId: new Uint8Array([10, 11]).buffer,
    type: "public-key",
    authenticatorAttachment: "platform",
    response: {
      clientDataJSON: clientData(creationChallenge),
      attestationObject: new Uint8Array([12, 13]).buffer,
      getTransports: () => ["internal"],
    },
    getClientExtensionResults: () => ({
      ...prf,
      hiddenFixture: prfBytes,
    }),
    toJSON: vi.fn(() => {
      throw new Error("The passkey serializer must not call toJSON");
    }),
  }) as unknown as PublicKeyCredential;

const assertionCredential = (
  prfOutput: Uint8Array,
  challenge = assertionChallenge,
): PublicKeyCredential =>
  ({
    id: "credential-id",
    rawId: new Uint8Array([10, 11]).buffer,
    type: "public-key",
    authenticatorAttachment: "platform",
    response: {
      clientDataJSON: clientData(challenge),
      authenticatorData: new Uint8Array([14, 15]).buffer,
      signature: new Uint8Array([16, 17]).buffer,
      userHandle: null,
    },
    getClientExtensionResults: () => ({
      prf: { enabled: true, results: { first: prfOutput.buffer } },
    }),
    toJSON: vi.fn(() => {
      throw new Error("The passkey serializer must not call toJSON");
    }),
  }) as unknown as PublicKeyCredential;

const installCredentials = (
  create: ReturnType<typeof vi.fn>,
  get: ReturnType<typeof vi.fn>,
): void => {
  Object.defineProperty(navigator, "credentials", {
    configurable: true,
    value: { create, get },
  });
};

afterEach(() => {
  vi.restoreAllMocks();
});

describe("passkey PRF vault wrapping", () => {
  it("uses the PRF returned during creation without an assertion", async () => {
    const prfOutput = new Uint8Array(32).fill(18);
    const create = vi.fn().mockResolvedValue(
      createCredential({ prf: { enabled: true, results: { first: prfOutput.buffer } } }),
    );
    const get = vi.fn();
    installCredentials(create, get);

    const result = await createPasskeyWithPrf({ creationOptions, assertionOptions, salt });

    expect(result.status).toBe("ready");
    expect(get).not.toHaveBeenCalled();
    expect(new Uint8Array((create.mock.calls[0][0].publicKey.extensions.prf.eval.first))).toEqual(
      salt,
    );
  });

  it("performs one assertion with the same salt when creation returns no PRF value", async () => {
    const create = vi.fn().mockResolvedValue(createCredential({ prf: { enabled: true } }));
    const get = vi.fn().mockResolvedValue(assertionCredential(new Uint8Array(32).fill(19)));
    installCredentials(create, get);

    const result = await createPasskeyWithPrf({ creationOptions, assertionOptions, salt });

    expect(result.status).toBe("ready");
    expect(get).toHaveBeenCalledOnce();
    expect(new Uint8Array(get.mock.calls[0][0].publicKey.extensions.prf.eval.first)).toEqual(salt);
  });

  it("returns prfUnsupported without creating a wrapper", async () => {
    const create = vi.fn().mockResolvedValue(createCredential({}));
    const get = vi.fn();
    installCredentials(create, get);

    const result = await createPasskeyWithPrf({ creationOptions, assertionOptions, salt });

    expect(result).toMatchObject({ status: "prfUnsupported" });
    expect("wrappingKey" in result).toBe(false);
    expect(get).not.toHaveBeenCalled();
  });

  it("derives independent wrapping keys for two credentials", async () => {
    const firstPrf = new Uint8Array(32).fill(20);
    const secondPrf = new Uint8Array(32).fill(21);
    const create = vi
      .fn()
      .mockResolvedValueOnce(
        createCredential({ prf: { enabled: true, results: { first: firstPrf.buffer } } }),
      )
      .mockResolvedValueOnce(
        createCredential({ prf: { enabled: true, results: { first: secondPrf.buffer } } }),
      );
    installCredentials(create, vi.fn());

    const first = await createPasskeyWithPrf({
      creationOptions,
      assertionOptions,
      salt: new Uint8Array(32).fill(22),
    });
    const second = await createPasskeyWithPrf({
      creationOptions,
      assertionOptions,
      salt: new Uint8Array(32).fill(23),
    });
    if (first.status !== "ready" || second.status !== "ready") {
      throw new Error("The passkey fixture must support PRF");
    }
    const plaintext = new TextEncoder().encode("master key");
    const additionalData = new TextEncoder().encode("passkey wrapper");
    const sealed = await encryptWithAdditionalData(first.wrappingKey, plaintext, additionalData);

    await expect(
      decryptWithAdditionalData(
        second.wrappingKey,
        sealed.nonce,
        sealed.ciphertext,
        additionalData,
      ),
    ).rejects.toBeDefined();
  });

  // The API requires clientExtensionResults to be present, so it is sent — but the PRF
  // result inside it derives the vault wrapping key and must never leave the browser.
  it("serializes credentials without the PRF extension result", () => {
    const prfOutput = new Uint8Array(32).fill(24);
    const credential = assertionCredential(prfOutput);
    const serialized = serializeAssertionForApi(credential);
    const json = JSON.stringify(serialized);

    expect(json).not.toContain("prf");
    expect(json).not.toContain(base64Url(prfOutput));
    expect(serialized.clientExtensionResults).not.toHaveProperty("prf");
    expect(credential.toJSON).not.toHaveBeenCalled();
    expect(serializeAttestationForApi(createCredential({ prf: { enabled: true } }))).toMatchObject({
      id: "credential-id",
      response: { transports: ["internal"] },
    });
  });

  it("rejects an assertion carrying a stale challenge", async () => {
    installCredentials(
      vi.fn(),
      vi
        .fn()
        .mockResolvedValue(
          assertionCredential(new Uint8Array(32).fill(25), new Uint8Array([99])),
        ),
    );

    await expect(requestPrfAssertion(assertionOptions, salt)).rejects.toThrow(
      "The passkey assertion challenge does not match",
    );
  });
});
