import { describe, expect, it } from "vitest";

const openDatabase = (name: string): Promise<IDBDatabase> =>
  new Promise((resolve, reject) => {
    const request = indexedDB.open(name, 1);
    request.onupgradeneeded = () => request.result.createObjectStore("records");
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });

describe("vault test environment", () => {
  it("provides Web Crypto with X25519 and AES-GCM", async () => {
    expect(crypto.subtle).toBeDefined();
    await expect(
      crypto.subtle.generateKey({ name: "X25519" }, false, ["deriveBits"]),
    ).resolves.toBeDefined();

    const key = await crypto.subtle.generateKey({ name: "AES-GCM", length: 256 }, false, [
      "encrypt",
      "decrypt",
    ]);
    const nonce = crypto.getRandomValues(new Uint8Array(12));
    const plaintext = new TextEncoder().encode("vault ready");
    const ciphertext = await crypto.subtle.encrypt({ name: "AES-GCM", iv: nonce }, key, plaintext);
    const decrypted = await crypto.subtle.decrypt({ name: "AES-GCM", iv: nonce }, key, ciphertext);

    expect(Array.from(new Uint8Array(decrypted))).toEqual(Array.from(plaintext));
  });

  it("provides IndexedDB", async () => {
    const database = await openDatabase("xpense-environment-test");
    expect([...database.objectStoreNames]).toContain("records");
    database.close();
    indexedDB.deleteDatabase("xpense-environment-test");
  });
});
