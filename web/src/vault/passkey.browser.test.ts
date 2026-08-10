import { afterEach, describe, expect, it, vi } from "vitest";
import { cdp } from "vitest/browser";
import {
  createPasskeyWithPrf,
  requestPrfAssertion,
  verifyPasskeyAssertionChallenge,
} from "./passkey";

interface VirtualAuthenticator {
  authenticatorId: string;
}

interface BrowserCdpSession {
  send(method: string, params?: object): Promise<unknown>;
}

interface PrfResults {
  prf?: {
    enabled?: boolean;
    results?: { first?: ArrayBuffer };
  };
}

const authenticators: string[] = [];

const browserCdp = (): BrowserCdpSession => cdp() as unknown as BrowserCdpSession;

const randomBytes = (length: number): Uint8Array<ArrayBuffer> =>
  crypto.getRandomValues(new Uint8Array(length));

const addAuthenticator = async (
  hasPrf: boolean,
  transport: "internal" | "usb" = "internal",
): Promise<string> => {
  await browserCdp().send("WebAuthn.enable");
  const result = await browserCdp().send("WebAuthn.addVirtualAuthenticator", {
    options: {
      protocol: "ctap2",
      ctap2Version: "ctap2_1",
      transport,
      hasResidentKey: true,
      hasUserVerification: true,
      isUserVerified: true,
      automaticPresenceSimulation: true,
      hasPrf,
    },
  }) as VirtualAuthenticator;
  authenticators.push(result.authenticatorId);
  return result.authenticatorId;
};

const creationOptions = (
  challenge: Uint8Array<ArrayBuffer>,
  userId: Uint8Array<ArrayBuffer>,
  salt: Uint8Array<ArrayBuffer>,
  attachment: AuthenticatorAttachment = "platform",
): CredentialCreationOptions => ({
  publicKey: {
    challenge,
    rp: { id: "localhost", name: "Xpense" },
    user: {
      id: userId,
      name: "browser-gate@example.test",
      displayName: "Browser gate",
    },
    pubKeyCredParams: [{ type: "public-key", alg: -7 }],
    authenticatorSelection: {
      authenticatorAttachment: attachment,
      residentKey: "required",
      requireResidentKey: true,
      userVerification: "required",
    },
    attestation: "none",
    extensions: { prf: { eval: { first: salt } } },
  },
});

const assertionOptions = (
  challenge: Uint8Array<ArrayBuffer>,
  allowCredentials: readonly PublicKeyCredential[] = [],
  transport: AuthenticatorTransport = "internal",
): CredentialRequestOptions => ({
  publicKey: {
    challenge,
    rpId: "localhost",
    allowCredentials: allowCredentials.map((credential) => ({
      id: credential.rawId,
      type: "public-key",
      transports: [transport],
    })),
    userVerification: "required",
  },
});

const createCredential = async (
  userId: Uint8Array<ArrayBuffer>,
  salt: Uint8Array<ArrayBuffer>,
  attachment: AuthenticatorAttachment = "platform",
): Promise<PublicKeyCredential> => {
  const credential = await navigator.credentials.create(
    creationOptions(randomBytes(32), userId, salt, attachment),
  );
  if (!(credential instanceof PublicKeyCredential)) {
    throw new Error("Chromium did not create a public-key credential");
  }
  return credential;
};

const prfBytes = (credential: PublicKeyCredential): Uint8Array<ArrayBuffer> | null => {
  const extension = credential.getClientExtensionResults() as PrfResults;
  const output = extension.prf?.results?.first;
  return output === undefined ? null : new Uint8Array(output);
};

afterEach(async () => {
  vi.restoreAllMocks();
  for (const authenticatorId of authenticators.splice(0)) {
    await browserCdp().send("WebAuthn.removeVirtualAuthenticator", { authenticatorId });
  }
  await browserCdp().send("WebAuthn.disable");
});

describe("passkey PRF browser contract", () => {
  it("creates a resident verified credential with a real 32-byte PRF result", async () => {
    await addAuthenticator(true);

    const credential = await createCredential(randomBytes(16), randomBytes(32));
    const extension = credential.getClientExtensionResults() as PrfResults;
    const output = prfBytes(credential);

    expect(extension.prf?.enabled).toBe(true);
    expect(output).toHaveLength(32);
    expect(new Set(output!).size).toBeGreaterThan(1);
  });

  it("reports unsupported PRF without deriving a passkey-only wrapper", async () => {
    await addAuthenticator(false);
    const salt = randomBytes(32);

    const result = await createPasskeyWithPrf({
      creationOptions: creationOptions(randomBytes(32), randomBytes(16), salt),
      assertionOptions: assertionOptions(randomBytes(32)),
      salt,
    });

    expect(result.status).toBe("prfUnsupported");
  });

  it("uses the allowed stripped creation-result seam before a real assertion", async () => {
    await addAuthenticator(true);
    const nativeCreate = navigator.credentials.create.bind(navigator.credentials);
    vi.spyOn(navigator.credentials, "create").mockImplementation(async (options) => {
      const credential = await nativeCreate(options);
      if (!(credential instanceof PublicKeyCredential)) return credential;
      Object.defineProperty(credential, "getClientExtensionResults", {
        configurable: true,
        value: () => ({ prf: { enabled: true } }),
      });
      return credential;
    });
    const salt = randomBytes(32);

    const result = await createPasskeyWithPrf({
      creationOptions: creationOptions(randomBytes(32), randomBytes(16), salt),
      assertionOptions: assertionOptions(randomBytes(32)),
      salt,
    });

    expect(result.status).toBe("ready");
    if (result.status !== "ready") throw new Error("The passkey was not ready");
    expect(result.assertionCredential).toBeInstanceOf(PublicKeyCredential);
    expect(prfBytes(result.assertionCredential!)).toHaveLength(32);
    expect(result.wrappingKey).toMatchObject({ algorithm: { name: "AES-GCM" } });
  });

  it("selects two credentials for one user handle and gets credential-specific outputs", async () => {
    const firstAuthenticator = await addAuthenticator(true, "usb");
    const userId = randomBytes(16);
    const first = await createCredential(userId, randomBytes(32), "cross-platform");
    await browserCdp().send("WebAuthn.setAutomaticPresenceSimulation", {
      authenticatorId: firstAuthenticator,
      enabled: false,
    });
    const secondAuthenticator = await addAuthenticator(true, "usb");
    const second = await createCredential(userId, randomBytes(32), "cross-platform");
    await browserCdp().send("WebAuthn.setAutomaticPresenceSimulation", {
      authenticatorId: secondAuthenticator,
      enabled: false,
    });
    await browserCdp().send("WebAuthn.setAutomaticPresenceSimulation", {
      authenticatorId: firstAuthenticator,
      enabled: true,
    });
    const salt = randomBytes(32);

    const firstAssertion = await requestPrfAssertion(
      assertionOptions(randomBytes(32), [first], "usb"),
      salt,
    );
    await browserCdp().send("WebAuthn.setAutomaticPresenceSimulation", {
      authenticatorId: firstAuthenticator,
      enabled: false,
    });
    await browserCdp().send("WebAuthn.setAutomaticPresenceSimulation", {
      authenticatorId: secondAuthenticator,
      enabled: true,
    });
    const secondAssertion = await requestPrfAssertion(
      assertionOptions(randomBytes(32), [second], "usb"),
      salt,
    );
    const firstOutput = prfBytes(firstAssertion.credential);
    const secondOutput = prfBytes(secondAssertion.credential);

    expect(new Uint8Array(firstAssertion.credential.rawId)).toEqual(new Uint8Array(first.rawId));
    expect(new Uint8Array(secondAssertion.credential.rawId)).toEqual(new Uint8Array(second.rawId));
    expect(firstOutput).toHaveLength(32);
    expect(secondOutput).toHaveLength(32);
    expect(firstOutput).not.toEqual(secondOutput);
  });

  it("rejects a real earlier assertion against a fresh client challenge", async () => {
    await addAuthenticator(true);
    const credential = await createCredential(randomBytes(16), randomBytes(32));
    const salt = randomBytes(32);
    const earlierChallenge = randomBytes(32);
    const staleOptions = assertionOptions(earlierChallenge, [credential]);
    const stale = await navigator.credentials.get({
      ...staleOptions,
      publicKey: {
        ...staleOptions.publicKey!,
        extensions: { prf: { eval: { first: salt } } },
      },
    });
    if (!(stale instanceof PublicKeyCredential)) {
      throw new Error("Chromium did not return a real assertion");
    }

    expect(() => verifyPasskeyAssertionChallenge(stale, randomBytes(32))).toThrow(
      "The passkey assertion challenge does not match",
    );
  });
});
