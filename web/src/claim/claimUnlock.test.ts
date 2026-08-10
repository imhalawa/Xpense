import { describe, expect, it, vi } from "vitest";
import {
  legacyClaimPasskeys,
  unlockLegacyClaimWithPasskey,
  type ClaimIdentity,
  type ClaimUnlockDependencies,
} from "./claimUnlock";

const userId = "11111111-1111-4111-8111-111111111111";
const firstWrapperId = "22222222-2222-4222-8222-222222222222";
const secondWrapperId = "33333333-3333-4333-8333-333333333333";

const wrapper = (id: string, credentialId: string, label: string) => ({
  id,
  kind: 0,
  credentialId,
  salt: "BAUG",
  ciphertext: "BwgJ",
  nonce: "CgsM",
  protocolVersion: 1,
  label,
});

const identity = (wrappers = [wrapper(firstWrapperId, "AQID", "Laptop")]): ClaimIdentity => ({
  id: userId,
  email: "owner@example.test",
  vaultWrappers: wrappers,
});

const assertionCredential = (): PublicKeyCredential => ({
  id: "AQID",
  rawId: new Uint8Array([1, 2, 3]).buffer,
  type: "public-key",
  response: {
    clientDataJSON: new Uint8Array([4]).buffer,
    authenticatorData: new Uint8Array([5]).buffer,
    signature: new Uint8Array([6]).buffer,
    userHandle: null,
  },
} as unknown as PublicKeyCredential);

const dependencies = (signedInIdentity = identity()): ClaimUnlockDependencies => ({
  identity: vi.fn().mockResolvedValue(identity()),
  options: vi.fn().mockResolvedValue({
    optionsJson: JSON.stringify({
      challenge: "BAUG",
      rpId: "configured.example.test",
      timeout: 30_000,
      userVerification: "required",
    }),
    pendingPasskeyAssertionId: "44444444-4444-4444-8444-444444444444",
  }),
  requestAssertion: vi.fn().mockResolvedValue({
    credential: assertionCredential(),
    wrappingKey: {} as CryptoKey,
  }),
  signIn: vi.fn().mockResolvedValue(signedInIdentity),
  unwrap: vi.fn().mockResolvedValue({} as CryptoKey),
  unlockWorker: vi.fn().mockResolvedValue(true),
});

describe("legacy claim passkey unlock", () => {
  it("uses server options, submits the assertion and unwraps the explicitly selected wrapper", async () => {
    const wrappingKey = {} as CryptoKey;
    const masterKey = {} as CryptoKey;
    const claimDependencies = dependencies();
    vi.mocked(claimDependencies.requestAssertion).mockResolvedValue({
      credential: assertionCredential(),
      wrappingKey,
    });
    vi.mocked(claimDependencies.unwrap).mockResolvedValue(masterKey);

    await expect(unlockLegacyClaimWithPasskey(claimDependencies, firstWrapperId)).resolves.toBe(true);

    expect(claimDependencies.options).toHaveBeenCalledWith("owner@example.test");
    expect(claimDependencies.requestAssertion).toHaveBeenCalledWith(
      expect.objectContaining({
        publicKey: expect.objectContaining({
          challenge: new Uint8Array([4, 5, 6]),
          rpId: "configured.example.test",
          allowCredentials: [{ type: "public-key", id: new Uint8Array([1, 2, 3]) }],
          userVerification: "required",
        }),
      }),
      new Uint8Array([4, 5, 6]),
    );
    expect(claimDependencies.signIn).toHaveBeenCalledWith(
      "44444444-4444-4444-8444-444444444444",
      expect.stringContaining("authenticatorData"),
    );
    expect(claimDependencies.unwrap).toHaveBeenCalledWith(
      wrappingKey,
      { nonce: new Uint8Array([10, 11, 12]), ciphertext: new Uint8Array([7, 8, 9]) },
      { userId, wrapperKind: "passkey", wrapperId: firstWrapperId },
    );
    expect(claimDependencies.unlockWorker).toHaveBeenCalledWith(masterKey, userId);
  });

  it("lists every usable passkey wrapper for explicit selection", () => {
    const account = identity([
      wrapper(firstWrapperId, "AQID", "Laptop"),
      wrapper(secondWrapperId, "BAUG", "Phone"),
    ]);

    expect(legacyClaimPasskeys(account)).toEqual([
      { id: firstWrapperId, label: "Laptop" },
      { id: secondWrapperId, label: "Phone" },
    ]);
  });

  it("refuses a sign-in response that does not contain the selected wrapper", async () => {
    const claimDependencies = dependencies(identity([
      wrapper(secondWrapperId, "BAUG", "Phone"),
    ]));

    await expect(unlockLegacyClaimWithPasskey(claimDependencies, firstWrapperId)).rejects.toThrow(
      "The selected passkey wrapper did not match the signed-in credential.",
    );

    expect(claimDependencies.unwrap).not.toHaveBeenCalled();
    expect(claimDependencies.unlockWorker).not.toHaveBeenCalled();
  });

  it("fails clearly when the selected wrapper is not available", async () => {
    const claimDependencies = dependencies();

    await expect(unlockLegacyClaimWithPasskey(claimDependencies, secondWrapperId)).rejects.toThrow(
      "The selected passkey vault wrapper is unavailable.",
    );

    expect(claimDependencies.options).not.toHaveBeenCalled();
  });
});
