import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { EncryptedRecordResult } from "../crypto/worker/commands";
import {
  deleteVaultDatabase,
  openVaultDatabase,
  type VaultDatabase,
  type VaultRecord,
} from "../vault/vaultDatabase";
import { hashClaimManifest } from "./manifest";
import {
  maximumPreparedClaimPayloadBytes,
  parseClaimDataset,
  prepareClaimRecords,
} from "./claimDataset";
import {
  runLegacyClaim,
  type ClaimApi,
  type ClaimRecordCipher,
  type ClaimStatus,
} from "./claimFlow";

const ownerId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const accountId = "11111111-1111-4111-8111-111111111111";
const transactionId = "22222222-2222-4222-8222-222222222222";
const notificationId = "44444444-4444-4444-8444-444444444444";
const timestamp = "2026-08-09T10:00:00Z";
const hiddenLabel = "Private legacy cash";
const bytes = (seed: number): Uint8Array => new Uint8Array([seed, seed + 1, seed + 2]);

const dataset = async (withTombstone = false): Promise<Record<string, unknown>> => {
  const identities = [
    { recordType: "account", id: accountId },
    ...(withTombstone ? [{ recordType: "transaction", id: transactionId }] : []),
  ];
  return {
    protocolVersion: 1,
    recordCount: identities.length,
    counts: {
      account: 1,
      necessityScale: 0,
      category: 0,
      merchant: 0,
      tag: 0,
      transaction: withTombstone ? 1 : 0,
      transfer: 0,
      budget: 0,
      notification: 0,
    },
    manifestHash: await hashClaimManifest(identities),
    accounts: [{
      id: accountId,
      legacyId: 1,
      isDeleted: false,
      createdAt: timestamp,
      updatedAt: null,
      label: hiddenLabel,
      accountNumber: "A-1",
      balanceMinorUnits: 1000,
      currency: 0,
      isDefault: true,
    }],
    necessityScales: [],
    categories: [],
    merchants: [],
    tags: [],
    transactions: withTombstone ? [{
      id: transactionId,
      legacyId: 2,
      isDeleted: true,
      createdAt: timestamp,
      updatedAt: null,
      kind: 1,
      amountMinorUnits: 50,
      currency: 0,
      occurredAt: timestamp,
      reason: null,
      sourceAccountLegacyId: 1,
      sourceAccountId: accountId,
      destinationAccountLegacyId: null,
      destinationAccountId: null,
      categoryLegacyId: null,
      categoryId: null,
      merchantLegacyId: null,
      merchantId: null,
      tagLegacyIds: [],
      tagIds: [],
    }] : [],
    budgets: [],
    notifications: [],
  };
};

const oversizedNotificationDataset = async (): Promise<Record<string, unknown>> => {
  const legacyDataset = await dataset();
  legacyDataset.recordCount = 2;
  (legacyDataset.counts as Record<string, number>).notification = 1;
  legacyDataset.manifestHash = await hashClaimManifest([
    { recordType: "account", id: accountId },
    { recordType: "notification", id: notificationId },
  ]);
  const notification = {
    id: notificationId,
    legacyId: 2,
    isDeleted: false,
    createdAt: timestamp,
    updatedAt: null,
    ownerLegacyId: null,
    eventId: "55555555-5555-4555-8555-555555555555",
    kind: 1,
    title: "Budget",
    message: "Exceeded",
    payload: "",
    payloadHash: "legacy-hash",
    readAt: null,
  };
  legacyDataset.notifications = [notification];
  const parsed = await parseClaimDataset(legacyDataset);
  const prepared = prepareClaimRecords(parsed).find((record) => record.id === notificationId)!;
  const emptyPayloadBytes = new TextEncoder().encode(JSON.stringify(prepared.payload)).byteLength;
  notification.payload = "x".repeat(maximumPreparedClaimPayloadBytes + 1 - emptyPayloadBytes);
  return legacyDataset;
};

const encrypted = (): EncryptedRecordResult => ({
  sealedPayload: { nonce: bytes(1), ciphertext: bytes(10) },
  personalEnvelope: { nonce: bytes(20), ciphertext: bytes(30) },
});

const serverRecord = (id: string, tombstone = false): VaultRecord => ({
  id,
  recordType: id === accountId ? "account" : "transaction",
  ownerId,
  parentResourceId: accountId,
  revision: 1,
  payload: bytes(10),
  tombstone,
  sequenceNumber: 1,
  serverCreatedAt: timestamp,
  serverUpdatedAt: timestamp,
});

const api = (legacyDataset: unknown): ClaimApi => ({
  currentUserId: vi.fn().mockResolvedValue(ownerId),
  start: vi.fn().mockResolvedValue({ claimToken: "memory-only-token", expiresAt: timestamp }),
  download: vi.fn().mockResolvedValue(legacyDataset),
  upload: vi.fn(async (request) => serverRecord(request.id)),
  remove: vi.fn().mockResolvedValue(undefined),
  complete: vi.fn().mockResolvedValue({
    recordCount: (legacyDataset as { recordCount?: number }).recordCount ?? 0,
    counts: (legacyDataset as { counts?: Record<string, number> }).counts ?? {},
    manifestHash: (legacyDataset as { manifestHash?: string }).manifestHash ?? "a".repeat(64),
    completedAt: timestamp,
  }),
});

const cipher = (ready = true): ClaimRecordCipher => ({
  isReady: () => ready,
  encrypt: vi.fn().mockResolvedValue(encrypted()),
});

let database: VaultDatabase;

beforeEach(async () => {
  await deleteVaultDatabase();
  database = await openVaultDatabase();
});

afterEach(async () => {
  database.close();
  await deleteVaultDatabase();
});

describe("legacy claim flow", () => {
  it("refuses before start when the Worker does not hold an unlocked master key", async () => {
    const claimApi = api(await dataset());

    await expect(runLegacyClaim({ api: claimApi, cipher: cipher(false), database }))
      .rejects.toThrow("Unlock the vault before starting the legacy claim.");

    expect(claimApi.start).not.toHaveBeenCalled();
  });

  it("validates the full dataset before invoking the encryption Worker", async () => {
    const invalid = await dataset();
    invalid.manifestHash = "0".repeat(64);
    const recordCipher = cipher();

    await expect(runLegacyClaim({ api: api(invalid), cipher: recordCipher, database }))
      .rejects.toThrow("The legacy dataset manifest does not match its records.");

    expect(recordCipher.encrypt).not.toHaveBeenCalled();
    await expect(database.records()).resolves.toEqual([]);
    await expect(database.outboxEntries()).resolves.toEqual([]);
  });

  it("rejects an oversized prepared notification before invoking the encryption Worker", async () => {
    const recordCipher = cipher();

    await expect(runLegacyClaim({
      api: api(await oversizedNotificationDataset()),
      cipher: recordCipher,
      database,
    })).rejects.toThrow(
      `Legacy notification ${notificationId}: The prepared encrypted payload must not exceed 65520 UTF-8 bytes.`,
    );

    expect(recordCipher.encrypt).not.toHaveBeenCalled();
    await expect(database.records()).resolves.toEqual([]);
    await expect(database.outboxEntries()).resolves.toEqual([]);
  });

  it("rejects a malformed authenticated user id before invoking the encryption Worker", async () => {
    const claimApi = api(await dataset());
    vi.mocked(claimApi.currentUserId).mockResolvedValue("not-a-user-id");
    const recordCipher = cipher();

    await expect(runLegacyClaim({ api: claimApi, cipher: recordCipher, database }))
      .rejects.toThrow("The authenticated user id is invalid.");

    expect(recordCipher.encrypt).not.toHaveBeenCalled();
    await expect(database.records()).resolves.toEqual([]);
    await expect(database.outboxEntries()).resolves.toEqual([]);
  });

  it("restarts from the exact staged ciphertext and stable idempotency key after upload interruption", async () => {
    const legacyDataset = await dataset();
    const firstApi = api(legacyDataset);
    vi.mocked(firstApi.upload).mockRejectedValueOnce(new Error("offline"));
    const firstCipher = cipher();

    await expect(runLegacyClaim({ api: firstApi, cipher: firstCipher, database }))
      .rejects.toThrow("offline");

    const [staged] = await database.outboxEntries();
    expect(staged).toMatchObject({
      operationId: `legacy-claim-create:${accountId}`,
      idempotencyKey: `legacy-claim-v1:${accountId}`,
      mutation: { kind: "create", record: { id: accountId } },
    });
    expect(JSON.stringify(staged)).not.toContain(hiddenLabel);
    expect(JSON.stringify(staged)).not.toContain("memory-only-token");
    expect(JSON.stringify(await database.records())).not.toContain(hiddenLabel);
    const originalPayload = Array.from(staged!.mutation.record.payload);
    const { sequence: _sequence, ...duplicateStage } = staged!;
    await database.enqueueOutboxOnce({
      ...duplicateStage,
      mutation: {
        ...duplicateStage.mutation,
        record: { ...duplicateStage.mutation.record, payload: bytes(90) },
      },
    });
    expect(Array.from((await database.getRecord(accountId))!.payload)).toEqual(originalPayload);
    await expect(database.outboxEntries()).resolves.toHaveLength(1);
    database.close();
    database = await openVaultDatabase();
    const resumedApi = api(legacyDataset);
    const resumedCipher = cipher();

    await runLegacyClaim({ api: resumedApi, cipher: resumedCipher, database });

    expect(resumedCipher.encrypt).not.toHaveBeenCalled();
    expect(vi.mocked(resumedApi.upload).mock.calls[0]![0].idempotencyKey)
      .toBe(`legacy-claim-v1:${accountId}`);
    expect(Array.from(vi.mocked(resumedApi.upload).mock.calls[0]![0].payload))
      .toEqual(originalPayload);
    await expect(database.outboxEntries()).resolves.toEqual([]);
  });

  it("creates roots before dependent records and only then applies tombstones", async () => {
    const claimApi = api(await dataset(true));
    const events: string[] = [];
    vi.mocked(claimApi.upload).mockImplementation(async (request) => {
      events.push(`create:${request.id}`);
      return serverRecord(request.id);
    });
    vi.mocked(claimApi.remove).mockImplementation(async (id) => {
      events.push(`delete:${id}`);
    });

    await runLegacyClaim({ api: claimApi, cipher: cipher(), database });

    expect(events).toEqual([
      `create:${accountId}`,
      `create:${transactionId}`,
      `delete:${transactionId}`,
    ]);
  });

  it("resumes an idempotent tombstone after a dropped delete response without re-encryption", async () => {
    const legacyDataset = await dataset(true);
    const firstApi = api(legacyDataset);
    vi.mocked(firstApi.remove).mockRejectedValueOnce(new Error("delete response dropped"));

    await expect(runLegacyClaim({ api: firstApi, cipher: cipher(), database }))
      .rejects.toThrow("delete response dropped");

    expect((await database.outboxEntryForRecord(transactionId))?.mutation.kind).toBe("delete");
    database.close();
    database = await openVaultDatabase();
    const resumedApi = api(legacyDataset);
    const resumedCipher = cipher();

    await runLegacyClaim({ api: resumedApi, cipher: resumedCipher, database });

    expect(resumedCipher.encrypt).not.toHaveBeenCalled();
    expect(resumedApi.upload).not.toHaveBeenCalled();
    expect(resumedApi.remove).toHaveBeenCalledWith(transactionId, undefined);
    expect((await database.getRecord(transactionId))?.tombstone).toBe(true);
  });

  it("aborts between records and reports only non-sensitive progress", async () => {
    const controller = new AbortController();
    const statuses: ClaimStatus[] = [];
    const claimApi = api(await dataset(true));
    vi.mocked(claimApi.upload).mockImplementation(async (request) => {
      if (request.id === accountId) controller.abort();
      return serverRecord(request.id);
    });

    await expect(runLegacyClaim({
      api: claimApi,
      cipher: cipher(),
      database,
      signal: controller.signal,
      onStatus: (status) => statuses.push(status),
    })).rejects.toMatchObject({ name: "AbortError" });

    expect(statuses.every((status) => !JSON.stringify(status).includes(hiddenLabel))).toBe(true);
    expect(claimApi.complete).not.toHaveBeenCalled();
  });
});
