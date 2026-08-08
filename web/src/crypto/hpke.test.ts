import {
  Aes128Gcm,
  CipherSuite,
  DhkemX25519HkdfSha256,
  HkdfSha256,
} from "@hpke/core";
import { describe, expect, it } from "vitest";
import {
  generateEncryptionIdentity,
  openWithPrivateKey,
  sealToPublicKey,
} from "./hpke";

const fromHex = (hex: string): Uint8Array =>
  Uint8Array.from(hex.match(/.{2}/g) ?? [], (pair) => Number.parseInt(pair, 16));
const toHex = (bytes: Uint8Array): string =>
  Array.from(bytes, (value) => value.toString(16).padStart(2, "0")).join("");
const copyAndFlip = (bytes: Uint8Array): Uint8Array => {
  const changed = bytes.slice();
  changed[0] ^= 1;
  return changed;
};

const additionalData = new TextEncoder().encode("v1|group|11111111-1111-4111-8111-111111111111");

describe("HPKE group key envelopes", () => {
  it("matches RFC 9180 Appendix A.1 base mode", async () => {
    const suite = new CipherSuite({
      kem: new DhkemX25519HkdfSha256(),
      kdf: new HkdfSha256(),
      aead: new Aes128Gcm(),
    });
    const recipient = await suite.kem.deriveKeyPair(
      fromHex("6db9df30aa07dd42ee5e8181afdb977e538f5e1fec8a06223f33f7013e525037"),
    );
    const sender = await suite.createSenderContext({
      recipientPublicKey: recipient.publicKey,
      info: fromHex("4f6465206f6e2061204772656369616e2055726e"),
      ekm: fromHex("7268600d403fce431561aef583ee1613527cff655c1343f29812e66706df3234"),
    });
    const ciphertext = await sender.seal(
      fromHex("4265617574792069732074727574682c20747275746820626561757479"),
      fromHex("436f756e742d30"),
    );

    expect(toHex(new Uint8Array(sender.enc))).toBe(
      "37fda3567bdbd628e88668c3c8d7e97d1d1253b6d4ea6d44c150f741f1bf4431",
    );
    expect(toHex(new Uint8Array(ciphertext))).toBe(
      "f938558b5d72f1a23810b4be2ab4f84331acc02fc97babc53a52ae8218a355a96d8770ac83d07bea87e13c512a",
    );
  });

  it("seals a group key to the intended recipient", async () => {
    const recipient = await generateEncryptionIdentity();
    const groupKey = crypto.getRandomValues(new Uint8Array(32));
    const envelope = await sealToPublicKey(recipient.publicKey, groupKey, additionalData);
    const opened = await openWithPrivateKey(
      recipient.privateKey,
      envelope.encapsulatedKey,
      envelope.ciphertext,
      additionalData,
    );

    expect(Array.from(opened)).toEqual(Array.from(groupKey));
  });

  it.each([
    ["encapsulated key", async () => {
      const recipient = await generateEncryptionIdentity();
      const envelope = await sealToPublicKey(
        recipient.publicKey,
        new Uint8Array(32),
        additionalData,
      );
      return openWithPrivateKey(
        recipient.privateKey,
        copyAndFlip(envelope.encapsulatedKey),
        envelope.ciphertext,
        additionalData,
      );
    }],
    ["ciphertext", async () => {
      const recipient = await generateEncryptionIdentity();
      const envelope = await sealToPublicKey(
        recipient.publicKey,
        new Uint8Array(32),
        additionalData,
      );
      return openWithPrivateKey(
        recipient.privateKey,
        envelope.encapsulatedKey,
        copyAndFlip(envelope.ciphertext),
        additionalData,
      );
    }],
    ["recipient", async () => {
      const recipient = await generateEncryptionIdentity();
      const stranger = await generateEncryptionIdentity();
      const envelope = await sealToPublicKey(
        recipient.publicKey,
        new Uint8Array(32),
        additionalData,
      );
      return openWithPrivateKey(
        stranger.privateKey,
        envelope.encapsulatedKey,
        envelope.ciphertext,
        additionalData,
      );
    }],
    ["additional data", async () => {
      const recipient = await generateEncryptionIdentity();
      const envelope = await sealToPublicKey(
        recipient.publicKey,
        new Uint8Array(32),
        additionalData,
      );
      return openWithPrivateKey(
        recipient.privateKey,
        envelope.encapsulatedKey,
        envelope.ciphertext,
        new TextEncoder().encode("v1|group|22222222-2222-4222-8222-222222222222"),
      );
    }],
  ] as const)("rejects a changed %s", async (_name, attempt) => {
    await expect(attempt()).rejects.toBeDefined();
  });
});
