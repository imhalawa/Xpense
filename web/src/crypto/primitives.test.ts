import { afterEach, describe, expect, it, vi } from "vitest";
import { payloadAdditionalData } from "./protocol";
import {
  decryptWithAdditionalData,
  deriveWrappingKey,
  encryptWithAdditionalData,
  generateSymmetricKey,
  importSymmetricKey,
} from "./primitives";

const fromHex = (hex: string): Uint8Array =>
  Uint8Array.from(hex.match(/.{2}/g) ?? [], (pair) => Number.parseInt(pair, 16));
const toHex = (bytes: Uint8Array): string =>
  Array.from(bytes, (value) => value.toString(16).padStart(2, "0")).join("");

const additionalData = payloadAdditionalData({
  recordId: "11111111-1111-4111-8111-111111111111",
  recordType: "transaction",
  ownerId: "22222222-2222-4222-8222-222222222222",
  revision: 1,
});

afterEach(() => vi.restoreAllMocks());

describe("symmetric primitives", () => {
  it("matches the first 32 bytes of RFC 5869 test case one", async () => {
    const inputKeyMaterial = fromHex("0b".repeat(22));
    const salt = fromHex("000102030405060708090a0b0c");
    const info = fromHex("f0f1f2f3f4f5f6f7f8f9");
    const expected =
      "3cb25f25faacd57a90434f64d0362f2a2d2d0a90cf1a5a4c5db02d56ecc4c5bf";

    const key = await deriveWrappingKey(inputKeyMaterial, salt, info, { extractable: true });
    expect(toHex(new Uint8Array(await crypto.subtle.exportKey("raw", key)))).toBe(expected);
  });

  it("matches the NIST AES-256-GCM zero vector", async () => {
    vi.spyOn(crypto, "getRandomValues").mockImplementation((array) => {
      new Uint8Array(array.buffer, array.byteOffset, array.byteLength).fill(0);
      return array;
    });
    const key = await importSymmetricKey(new Uint8Array(32), { extractable: false });
    const sealed = await encryptWithAdditionalData(key, new Uint8Array(16), new Uint8Array());

    expect(toHex(sealed.nonce)).toBe("00".repeat(12));
    expect(toHex(sealed.ciphertext)).toBe(
      "cea7403d4d606b6e074ec5d3baf39d18d0d1c8a799996bf0265b98b5d48ab919",
    );
  });

  it("round-trips with authenticated record data", async () => {
    const key = await generateSymmetricKey();
    const plaintext = new TextEncoder().encode("encrypted transaction");
    const sealed = await encryptWithAdditionalData(key, plaintext, additionalData);
    const opened = await decryptWithAdditionalData(
      key,
      sealed.nonce,
      sealed.ciphertext,
      additionalData,
    );
    expect(Array.from(opened)).toEqual(Array.from(plaintext));
  });

  it.each([
    ["ciphertext", (nonce: Uint8Array, ciphertext: Uint8Array) => {
      ciphertext[0] ^= 1;
      return { nonce, ciphertext, additionalData };
    }],
    ["nonce", (nonce: Uint8Array, ciphertext: Uint8Array) => {
      nonce[0] ^= 1;
      return { nonce, ciphertext, additionalData };
    }],
    ["additional data", (nonce: Uint8Array, ciphertext: Uint8Array) => ({
      nonce,
      ciphertext,
      additionalData: payloadAdditionalData({
        recordId: "11111111-1111-4111-8111-111111111111",
        recordType: "transaction",
        ownerId: "22222222-2222-4222-8222-222222222222",
        revision: 2,
      }),
    })],
    ["authentication tag", (nonce: Uint8Array, ciphertext: Uint8Array) => ({
      nonce,
      ciphertext: ciphertext.slice(0, -1),
      additionalData,
    })],
  ] as const)("rejects tampered %s", async (_name, tamper) => {
    const key = await generateSymmetricKey();
    const sealed = await encryptWithAdditionalData(
      key,
      new TextEncoder().encode("private"),
      additionalData,
    );
    const changed = tamper(sealed.nonce.slice(), sealed.ciphertext.slice());
    await expect(
      decryptWithAdditionalData(
        key,
        changed.nonce,
        changed.ciphertext,
        changed.additionalData,
      ),
    ).rejects.toBeDefined();
  });

  it("uses fresh 96-bit nonces", async () => {
    const key = await generateSymmetricKey();
    const first = await encryptWithAdditionalData(key, new Uint8Array([1]), additionalData);
    const second = await encryptWithAdditionalData(key, new Uint8Array([1]), additionalData);
    expect(first.nonce).toHaveLength(12);
    expect(second.nonce).toHaveLength(12);
    expect(Array.from(first.nonce)).not.toEqual(Array.from(second.nonce));
  });

  it("keeps generated symmetric keys non-extractable", async () => {
    const key = await generateSymmetricKey();
    expect(key.extractable).toBe(false);
    await expect(crypto.subtle.exportKey("raw", key)).rejects.toBeDefined();
  });
});
