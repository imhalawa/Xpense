import { afterEach, beforeEach, describe, expect, it } from "vitest";
import {
  VAULT_DATABASE_NAME,
  deleteVaultDatabase,
  openVaultDatabase,
  type VaultDatabase,
  type VaultStoreName,
} from "./vaultDatabase";

const recordId = "11111111-1111-4111-8111-111111111111";
const wrapperId = "22222222-2222-4222-8222-222222222222";
const operationId = "33333333-3333-4333-8333-333333333333";
const hiddenLabel = "Private shop purchase";
const bytes = (seed: number): Uint8Array => new Uint8Array([seed, seed + 1, seed + 2]);
const isByteArray = (value: unknown): value is Uint8Array =>
  ArrayBuffer.isView(value) && value.constructor.name === "Uint8Array";
const normalise = (value: unknown): unknown => {
  if (isByteArray(value)) return Array.from(value);
  if (Array.isArray(value)) return value.map(normalise);
  if (value !== null && typeof value === "object") {
    return Object.fromEntries(Object.entries(value).map(([key, entry]) => [key, normalise(entry)]));
  }
  return value;
};

const fixtures: Array<{
  store: VaultStoreName;
  key: IDBValidKey;
  value: Record<string, unknown>;
}> = [
  {
    store: "records",
    key: recordId,
    value: {
      id: recordId,
      type: "transaction",
      ownerId: wrapperId,
      revision: 1,
      nonce: bytes(1),
      ciphertext: bytes(10),
      tombstone: false,
      serverCreatedAt: "2026-08-08T08:00:00Z",
      serverUpdatedAt: "2026-08-08T08:00:00Z",
    },
  },
  {
    store: "envelopes",
    key: [recordId, "personal"],
    value: {
      recordId,
      groupId: "personal",
      wrappedKey: bytes(20),
      nonce: bytes(30),
      protocolVersion: 1,
    },
  },
  {
    store: "wrappers",
    key: wrapperId,
    value: {
      id: wrapperId,
      kind: "recoveryFile",
      nonce: bytes(40),
      ciphertext: bytes(50),
    },
  },
  {
    store: "syncState",
    key: "current",
    value: { key: "current", cursor: "opaque-cursor", lastSyncTime: "2026-08-08T08:00:00Z" },
  },
  {
    store: "outbox",
    key: operationId,
    value: {
      operationId,
      idempotencyKey: "operation-1",
      mutation: { ciphertext: bytes(60) },
    },
  },
  {
    store: "quarantine",
    key: recordId,
    value: { recordId, ciphertext: bytes(70), reason: "authentication-failed" },
  },
];

let database: VaultDatabase | undefined;

beforeEach(async () => {
  await deleteVaultDatabase();
  database = await openVaultDatabase();
});

afterEach(async () => {
  database?.close();
  database = undefined;
  await deleteVaultDatabase();
});

describe("vault ciphertext database", () => {
  it.each(fixtures)("puts, gets, and deletes from $store", async ({ store, key, value }) => {
    await database!.put(store, value);
    expect(normalise(await database!.get(store, key))).toEqual(normalise(value));
    await database!.delete(store, key);
    await expect(database!.get(store, key)).resolves.toBeUndefined();
  });

  it("keeps the sync cursor after close and reopen", async () => {
    await database!.put("syncState", {
      key: "current",
      cursor: "next-page-token",
      lastSyncTime: "2026-08-08T08:00:00Z",
    });
    database!.close();

    database = await openVaultDatabase();

    await expect(database.get("syncState", "current")).resolves.toMatchObject({
      cursor: "next-page-token",
    });
  });

  it("creates all six stores when no database exists", () => {
    expect(Array.from(database!.objectStoreNames).sort()).toEqual(
      ["records", "envelopes", "wrappers", "syncState", "outbox", "quarantine"].sort(),
    );
  });

  it("stores no plaintext or cryptographic keys", async () => {
    for (const fixture of fixtures) await database!.put(fixture.store, fixture.value);
    const stored = await Promise.all(
      fixtures.map((fixture) => database!.get(fixture.store, fixture.key)),
    );
    const key = await crypto.subtle.generateKey({ name: "AES-GCM", length: 256 }, false, [
      "encrypt",
      "decrypt",
    ]);
    const keyConstructor = key.constructor;
    const inspect = (value: unknown): void => {
      expect(value).not.toBeInstanceOf(keyConstructor);
      if (typeof value === "string") {
        expect(value).not.toContain(hiddenLabel);
        return;
      }
      if (isByteArray(value)) {
        expect(new TextDecoder().decode(value)).not.toContain(hiddenLabel);
        return;
      }
      if (Array.isArray(value)) {
        value.forEach(inspect);
        return;
      }
      if (value !== null && typeof value === "object") {
        Object.values(value).forEach(inspect);
      }
    };

    stored.forEach(inspect);
  });
});
