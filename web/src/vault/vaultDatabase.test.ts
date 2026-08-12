import { afterEach, beforeEach, describe, expect, it } from "vitest";
import {
  deleteVaultDatabase,
  openVaultDatabase,
  type VaultDatabase,
  type VaultRecord,
  type VaultStoreName,
} from "./vaultDatabase";

const recordId = "11111111-1111-4111-8111-111111111111";
const ownerId = "22222222-2222-4222-8222-222222222222";
const operationId = "33333333-3333-4333-8333-333333333333";
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

const makeRecord = (id: string, recordType: VaultRecord["recordType"]): VaultRecord => ({
  id,
  recordType,
  ownerId,
  parentResourceId: recordType === "budget" ? id : null,
  revision: 1,
  payload: bytes(5),
  tombstone: false,
  sequenceNumber: 1,
  serverCreatedAt: "2026-08-09T10:00:00Z",
  serverUpdatedAt: "2026-08-09T10:00:00Z",
});

const seedVersionThreeDatabase = (): Promise<void> =>
  new Promise((resolve, reject) => {
    const request = indexedDB.open("xpense-vault", 3);
    request.onupgradeneeded = () => {
      request.result.createObjectStore("records", { keyPath: "id" });
      request.result.createObjectStore("envelopes", { keyPath: ["recordId", "groupId"] });
      request.result.createObjectStore("wrappers", { keyPath: "id" });
      request.result.createObjectStore("syncState", { keyPath: "key" });
      request.result.createObjectStore("outbox", { keyPath: "operationId" });
      request.result.createObjectStore("quarantine", { keyPath: ["recordId", "revision"] });
    };
    request.onsuccess = () => {
      request.result.close();
      resolve();
    };
    request.onerror = () => reject(request.error);
  });

const fixtures: Array<{
  store: VaultStoreName;
  key: IDBValidKey;
  value: Record<string, unknown>;
}> = [
  { store: "records", key: recordId, value: { ...makeRecord(recordId, "transaction") } },
  {
    store: "syncState",
    key: "changes",
    value: { key: "changes", cursor: "opaque-cursor" },
  },
  {
    store: "outbox",
    key: operationId,
    value: {
      operationId,
      idempotencyKey: "operation-1",
      sequence: 1,
      mutation: { kind: "delete", record: makeRecord(recordId, "transaction") },
    },
  },
  {
    store: "quarantine",
    key: [recordId, 1],
    value: { recordId, revision: 1, reason: "invalid-payload" },
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

describe("vault record database", () => {
  it("atomically rejects a batch when one outbox record cannot be stored", async () => {
    const categoryId = "88888888-8888-4888-8888-888888888888";
    const budgetId = "99999999-9999-4999-8999-999999999999";
    const category = makeRecord(categoryId, "category");
    const budget = makeRecord(budgetId, "budget");
    await database!.putRecord(category);
    await database!.putRecord(budget);
    const invalidBudget = {
      ...budget,
      tombstone: true,
      payload: (() => undefined) as unknown as Uint8Array,
    };

    await expect(database!.enqueueOutboxBatch([
      { operationId: crypto.randomUUID(), idempotencyKey: crypto.randomUUID(), mutation: { kind: "delete", record: { ...category, tombstone: true } } },
      { operationId: crypto.randomUUID(), idempotencyKey: crypto.randomUUID(), mutation: { kind: "delete", record: invalidBudget } },
    ])).rejects.toBeDefined();

    await expect(database!.outboxEntries()).resolves.toEqual([]);
    await expect(database!.getRecord(categoryId)).resolves.toMatchObject({ tombstone: false });
    await expect(database!.getRecord(budgetId)).resolves.toMatchObject({ tombstone: false });
  });

  it.each(fixtures)("puts, gets, and deletes from $store", async ({ store, key, value }) => {
    await database!.put(store, value);
    expect(normalise(await database!.get(store, key))).toEqual(normalise(value));
    await database!.delete(store, key);
    await expect(database!.get(store, key)).resolves.toBeUndefined();
  });

  it("round-trips a record payload unchanged", async () => {
    const stored = makeRecord(recordId, "account");

    await database!.putRecord(stored);

    expect(normalise(await database!.getRecord(recordId))).toEqual(normalise(stored));
  });

  it("keeps the sync cursor after close and reopen", async () => {
    await database!.putSyncState({ key: "changes", cursor: "next-page-token" });
    database!.close();

    database = await openVaultDatabase();

    await expect(database.getSyncState()).resolves.toMatchObject({ cursor: "next-page-token" });
  });

  it("creates only the four local-first stores when no database exists", () => {
    expect(Array.from(database!.objectStoreNames).sort()).toEqual(
      ["outbox", "quarantine", "records", "syncState"],
    );
  });

  it("drops the encrypted stores when upgrading from the ciphertext schema", async () => {
    database!.close();
    await deleteVaultDatabase();
    await seedVersionThreeDatabase();

    database = await openVaultDatabase();

    expect(database.version).toBe(4);
    expect(Array.from(database.objectStoreNames).sort()).toEqual(
      ["outbox", "quarantine", "records", "syncState"],
    );
  });
});
