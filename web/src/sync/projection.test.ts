import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import axios from "axios";
import { rebuildProjection } from "./projection";
import {
  deleteVaultDatabase,
  openVaultDatabase,
  type VaultDatabase,
  type VaultRecord,
} from "../vault/vaultDatabase";
import { Currency } from "../typings/enums/Currency";

vi.mock("axios");

const ownerId = "11111111-1111-4111-8111-111111111111";
const accountId = "22222222-2222-4222-8222-222222222222";
const badId = "33333333-3333-4333-8333-333333333333";
const legacyQuarantineId = "55555555-5555-4555-8555-555555555555";
const encoder = new TextEncoder();

const record = (id: string, tombstone = false, revision = 1): VaultRecord => ({
  id,
  recordType: "account",
  ownerId,
  parentResourceId: null,
  revision,
  protocolVersion: 1,
  nonce: new Uint8Array([1]),
  ciphertext: new Uint8Array([2]),
  envelopes: [],
  tombstone,
  sequenceNumber: 1,
  serverCreatedAt: "2026-08-09T10:00:00Z",
  serverUpdatedAt: "2026-08-09T10:00:00Z",
});

const accountPayload = (id: string, label = "Cash"): Uint8Array =>
  encoder.encode(JSON.stringify({
    accountNumber: id,
    label,
    balance: { minorUnits: 1250, currency: Currency.EUR },
    isDefault: true,
    createdAt: "2026-08-09T10:00:00Z",
    updatedAt: null,
  }));

const seedVersionOneDatabase = (
  legacyRecords: readonly object[],
  legacyEnvelopes: readonly object[],
  legacyQuarantine: object,
): Promise<void> =>
  new Promise((resolve, reject) => {
    const request = indexedDB.open("xpense-vault", 1);
    request.onupgradeneeded = () => {
      const records = request.result.createObjectStore("records", { keyPath: "id" });
      const envelopes = request.result.createObjectStore("envelopes", {
        keyPath: ["recordId", "groupId"],
      });
      request.result.createObjectStore("wrappers", { keyPath: "id" });
      request.result.createObjectStore("syncState", { keyPath: "key" });
      request.result.createObjectStore("outbox", { keyPath: "operationId" });
      const quarantine = request.result.createObjectStore("quarantine", { keyPath: "recordId" });
      legacyRecords.forEach((legacyRecord) => records.put(legacyRecord));
      legacyEnvelopes.forEach((legacyEnvelope) => envelopes.put(legacyEnvelope));
      quarantine.put(legacyQuarantine);
    };
    request.onsuccess = () => {
      request.result.close();
      resolve();
    };
    request.onerror = () => reject(request.error);
  });

let database: VaultDatabase | undefined;

beforeEach(async () => {
  vi.mocked(axios.get).mockReset();
  await deleteVaultDatabase();
  database = await openVaultDatabase();
});

afterEach(async () => {
  database?.close();
  database = undefined;
  await deleteVaultDatabase();
});

describe("rebuildProjection", () => {
  it("rebuilds legacy page view models from encrypted IndexedDB records without network access", async () => {
    await database!.putRecord(record(accountId));
    await database!.putRecord(record(badId));
    await database!.putRecord(record("44444444-4444-4444-8444-444444444444", true));
    await database!.putQuarantine({
      recordId: badId,
      revision: 2,
      record: record(badId, false, 2),
      reason: "authentication-failed",
    });
    const decrypt = vi.fn(async (stored: VaultRecord) => accountPayload(stored.id));

    const projection = await rebuildProjection(database!, { decrypt });

    expect(projection.accounts.map((account) => account.accountNumber)).toEqual([accountId, badId]);
    expect(projection.categories).toEqual([]);
    expect(decrypt).toHaveBeenCalledTimes(2);
    expect(axios.get).not.toHaveBeenCalled();
  });

  it("upgrades exact version-one records, joins separate envelopes, and preserves legacy quarantine", async () => {
    database!.close();
    database = undefined;
    await deleteVaultDatabase();
    const legacyRecord = (id: string) => ({
      id,
      type: "account",
      ownerId,
      revision: 1,
      nonce: new Uint8Array([1, 2, 3]),
      ciphertext: new Uint8Array([4, 5, 6]),
      tombstone: false,
      serverCreatedAt: "2026-08-08T08:00:00Z",
      serverUpdatedAt: "2026-08-08T08:00:00Z",
    });
    const legacyEnvelope = (id: string) => ({
      recordId: id,
      groupId: "personal",
      wrappedKey: new Uint8Array([7, 8, 9]),
      nonce: new Uint8Array([10, 11, 12]),
      protocolVersion: 1,
    });
    const legacyQuarantine = {
      recordId: legacyQuarantineId,
      ciphertext: new Uint8Array([13, 14, 15]),
      reason: "authentication-failed",
    };
    await seedVersionOneDatabase(
      [legacyRecord(accountId), legacyRecord(legacyQuarantineId)],
      [legacyEnvelope(accountId), legacyEnvelope(legacyQuarantineId)],
      legacyQuarantine,
    );

    database = await openVaultDatabase();
    const decrypt = vi.fn().mockResolvedValue(accountPayload(accountId, "Legacy cash"));
    const projection = await rebuildProjection(database, { decrypt });

    expect(database.version).toBe(2);
    const upgraded = await database.getRecord(accountId);
    expect(upgraded).toBeDefined();
    expect({
      ...upgraded,
      nonce: Array.from(upgraded!.nonce),
      ciphertext: Array.from(upgraded!.ciphertext),
      envelopes: upgraded!.envelopes.map((envelope) => ({
        ...envelope,
        wrappedKey: Array.from(envelope.wrappedKey),
        nonce: Array.from(envelope.nonce),
      })),
    }).toEqual({
      id: accountId,
      recordType: "account",
      ownerId,
      parentResourceId: null,
      revision: 1,
      protocolVersion: 1,
      nonce: [1, 2, 3],
      ciphertext: [4, 5, 6],
      envelopes: [{
        id: `${accountId}:personal`,
        groupId: null,
        wrappedKey: [7, 8, 9],
        nonce: [10, 11, 12],
        encapsulatedKey: null,
        protocolVersion: 1,
      }],
      tombstone: false,
      sequenceNumber: 0,
      serverCreatedAt: "2026-08-08T08:00:00Z",
      serverUpdatedAt: "2026-08-08T08:00:00Z",
    });
    expect(projection.accounts).toContainEqual(expect.objectContaining({
      accountNumber: accountId,
      label: "Legacy cash",
    }));
    expect(decrypt).toHaveBeenCalledOnce();
    const upgradedQuarantine = await database.getQuarantine(legacyQuarantineId, 0);
    expect({
      ...upgradedQuarantine,
      ciphertext: Array.from(upgradedQuarantine!.ciphertext!),
    }).toEqual({
      ...legacyQuarantine,
      ciphertext: [13, 14, 15],
      revision: 0,
    });
    expect(JSON.stringify(await database.get("records", accountId))).not.toContain("Legacy cash");
  });

  it("quarantines an authenticated payload with malformed view-model fields", async () => {
    await database!.putRecord(record(accountId));
    const hiddenText = "must never be trusted UI data";
    const decrypt = vi.fn().mockResolvedValue(encoder.encode(JSON.stringify({
      accountNumber: 7,
      label: hiddenText,
      balance: { minorUnits: "many", currency: Currency.EUR },
      isDefault: true,
      createdAt: "2026-08-09T10:00:00Z",
      updatedAt: null,
    })));

    const projection = await rebuildProjection(database!, { decrypt });

    expect(projection.accounts).toEqual([]);
    const quarantined = await database!.getQuarantine(accountId, 1);
    expect(quarantined).toMatchObject({
      recordId: accountId,
      revision: 1,
      reason: "invalid-payload",
    });
    expect(JSON.stringify(quarantined)).not.toContain(hiddenText);
  });
});
