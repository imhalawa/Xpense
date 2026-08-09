import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import axios, { type AxiosAdapter } from "axios";
import {
  SyncClient,
  addSyncRecordEnvelope,
  createSyncRecords,
  deleteSyncRecord,
  getSyncChanges,
  replaceSyncRecord,
  revokeSyncRecordEnvelope,
  type SyncRecord,
} from "./syncClient";
import {
  deleteVaultDatabase,
  openVaultDatabase,
  type VaultDatabase,
  type VaultOutboxMutation,
  type VaultRecord,
} from "../vault/vaultDatabase";

vi.mock("axios");

const ownerId = "11111111-1111-4111-8111-111111111111";
const recordId = "22222222-2222-4222-8222-222222222222";
const envelopeId = "33333333-3333-4333-8333-333333333333";
const groupId = "44444444-4444-4444-8444-444444444444";
const bytes = (seed: number): Uint8Array => new Uint8Array([seed, seed + 1, seed + 2]);
const accountPayload = (id = recordId): Uint8Array =>
  new TextEncoder().encode(JSON.stringify({
    accountNumber: id,
    label: "Cash",
    balance: { minorUnits: 1250, currency: "EUR" },
    isDefault: true,
    createdAt: "2026-08-09T10:00:00Z",
    updatedAt: null,
  }));

const record = (revision: number, overrides: Partial<SyncRecord> = {}): SyncRecord => ({
  id: recordId,
  recordType: "account",
  ownerId,
  parentResourceId: null,
  revision,
  protocolVersion: 1,
  nonce: bytes(10 + revision),
  ciphertext: bytes(20 + revision),
  envelopes: [
    {
      id: envelopeId,
      groupId: null,
      wrappedKey: bytes(30 + revision),
      nonce: bytes(40 + revision),
      encapsulatedKey: null,
      protocolVersion: 1,
    },
  ],
  tombstone: false,
  sequenceNumber: revision,
  serverCreatedAt: "2026-08-09T10:00:00Z",
  serverUpdatedAt: "2026-08-09T10:00:00Z",
  ...overrides,
});

const wireRecord = (revision: number, overrides: Partial<SyncRecord> = {}) => {
  const value = record(revision, overrides);
  return {
    id: value.id,
    recordType: 0,
    ownerUserId: value.ownerId,
    parentResourceId: value.parentResourceId,
    revision: value.revision,
    protocolVersion: value.protocolVersion,
    nonce: value.nonce,
    ciphertext: value.ciphertext,
    envelopes: value.envelopes,
    isDeleted: value.tombstone,
    sequenceNumber: value.sequenceNumber,
    createdAt: value.serverCreatedAt,
    updatedAt: value.serverUpdatedAt,
  };
};

let database: VaultDatabase | undefined;

beforeEach(async () => {
  vi.mocked(axios.get).mockReset();
  vi.mocked(axios.post).mockReset();
  vi.mocked(axios.put).mockReset();
  vi.mocked(axios.delete).mockReset();
  await deleteVaultDatabase();
  database = await openVaultDatabase();
});

afterEach(async () => {
  database?.close();
  database = undefined;
  await deleteVaultDatabase();
});

describe("SyncClient.pull", () => {
  it("zeroes authenticated plaintext immediately after pull validation", async () => {
    const decrypted = accountPayload();
    vi.mocked(axios.get).mockResolvedValue({
      data: { records: [wireRecord(1)], nextCursor: "complete", hasMore: false },
    });

    await new SyncClient(database!, { decrypt: vi.fn().mockResolvedValue(decrypted) }).pull();

    expect([...decrypted].every((value) => value === 0)).toBe(true);
  });

  it("stages a pulled server revision without overwriting queued optimistic ciphertext", async () => {
    const optimistic: VaultRecord = record(1, {
      nonce: bytes(80),
      ciphertext: bytes(90),
    });
    const mutation: VaultOutboxMutation = {
      kind: "replace",
      record: optimistic,
      request: {
        expectedRevision: 1,
        protocolVersion: 1,
        nonce: optimistic.nonce,
        ciphertext: optimistic.ciphertext,
      },
    };
    await database!.enqueueOutbox({
      operationId: "55555555-5555-4555-8555-555555555555",
      idempotencyKey: "66666666-6666-4666-8666-666666666666",
      mutation,
    });
    vi.mocked(axios.get).mockResolvedValue({
      data: { records: [wireRecord(2)], nextCursor: "staged", hasMore: false },
    });

    await new SyncClient(database!, { decrypt: vi.fn().mockResolvedValue(accountPayload()) }).pull();

    const local = await database!.getRecord(recordId);
    expect(local).toMatchObject({ revision: 1 });
    expect(Array.from(local!.ciphertext)).toEqual(Array.from(bytes(90)));
    await expect(database!.outboxEntries()).resolves.toEqual([
      expect.objectContaining({
        mutation: expect.objectContaining({ record: expect.objectContaining({ revision: 1 }) }),
        latestServerRecord: expect.objectContaining({ revision: 2 }),
      }),
    ]);
    await expect(database!.getSyncState()).resolves.toEqual({ key: "changes", cursor: "staged" });
  });

  it("applies created, updated, and tombstoned records in cursor-page order", async () => {
    vi.mocked(axios.get)
      .mockResolvedValueOnce({
        data: {
          records: [
            wireRecord(1, {
              envelopes: [
                record(1).envelopes[0],
                {
                  id: groupId,
                  groupId,
                  wrappedKey: bytes(50),
                  nonce: bytes(60),
                  encapsulatedKey: bytes(70),
                  protocolVersion: 1,
                },
              ],
            }),
          ],
          nextCursor: "first",
          hasMore: true,
        },
      })
      .mockImplementationOnce(async () => {
        await expect(database!.getSyncState()).resolves.toEqual({ key: "changes", cursor: "first" });
        return {
          data: {
            records: [wireRecord(2, { tombstone: true })],
            nextCursor: "second",
            hasMore: false,
          },
        };
      });
    const decrypt = vi.fn().mockResolvedValue(bytes(90));

    await new SyncClient(database!, { decrypt }).pull();

    expect(axios.get).toHaveBeenNthCalledWith(1, "/api/v1/sync/changes", {
      params: { cursor: undefined },
      signal: undefined,
    });
    expect(axios.get).toHaveBeenNthCalledWith(2, "/api/v1/sync/changes", {
      params: { cursor: "first" },
      signal: undefined,
    });
    await expect(database!.getRecord(recordId)).resolves.toMatchObject({
      revision: 2,
      tombstone: true,
    });
    await expect(database!.getSyncState()).resolves.toEqual({ key: "changes", cursor: "second" });
    expect(decrypt).toHaveBeenCalledTimes(1);
    expect(decrypt.mock.calls[0][0].envelopes).toContainEqual(
      expect.objectContaining({ groupId, wrappedKey: bytes(50), encapsulatedKey: bytes(70) }),
    );
  });

  it("resumes from the persisted cursor after a reload", async () => {
    await database!.putSyncState({ key: "changes", cursor: "saved" });
    database!.close();
    database = await openVaultDatabase();
    vi.mocked(axios.get).mockResolvedValue({
      data: { records: [], nextCursor: "saved", hasMore: false },
    });

    await new SyncClient(database!, { decrypt: vi.fn() }).pull();

    expect(axios.get).toHaveBeenCalledWith("/api/v1/sync/changes", {
      params: { cursor: "saved" },
      signal: undefined,
    });
  });

  it("quarantines an unauthentic revision without replacing the last good record", async () => {
    const good: VaultRecord = record(1);
    await database!.putRecord(good);
    vi.mocked(axios.get).mockResolvedValue({
      data: { records: [wireRecord(2)], nextCursor: "after-bad", hasMore: false },
    });

    await new SyncClient(database!, { decrypt: vi.fn().mockRejectedValue(new Error("bad tag")) }).pull();

    await expect(database!.getRecord(recordId)).resolves.toMatchObject({ revision: 1 });
    await expect(database!.getQuarantine(recordId, 2)).resolves.toMatchObject({
      recordId,
      revision: 2,
      reason: "authentication-failed",
      record: { revision: 2 },
    });
    await expect(database!.getSyncState()).resolves.toEqual({ key: "changes", cursor: "after-bad" });
  });

  it("does not make progress when cancellation is already requested", async () => {
    const controller = new AbortController();
    controller.abort();

    await expect(new SyncClient(database!, { decrypt: vi.fn() }).pull(controller.signal)).rejects.toThrow(
      "The sync was cancelled",
    );

    expect(axios.get).not.toHaveBeenCalled();
    await expect(database!.getSyncState()).resolves.toBeUndefined();
  });

  it("does not quarantine or advance the cursor when cancellation interrupts decryption", async () => {
    const controller = new AbortController();
    vi.mocked(axios.get).mockResolvedValue({
      data: { records: [wireRecord(1)], nextCursor: "complete", hasMore: false },
    });
    const decrypt = vi.fn(async () => {
      controller.abort();
      throw new Error("cancelled");
    });

    await expect(new SyncClient(database!, { decrypt }).pull(controller.signal)).rejects.toThrow(
      "The sync was cancelled",
    );

    await expect(database!.getRecord(recordId)).resolves.toBeUndefined();
    await expect(database!.getQuarantine(recordId, 1)).resolves.toBeUndefined();
    await expect(database!.getSyncState()).resolves.toBeUndefined();
  });

  it("rolls back the page when cancellation arrives while decryption is pending", async () => {
    const controller = new AbortController();
    let finishDecrypt: ((value: Uint8Array) => void) | undefined;
    vi.mocked(axios.get).mockResolvedValue({
      data: { records: [wireRecord(1)], nextCursor: "complete", hasMore: false },
    });
    const decrypt = vi.fn(() => new Promise<Uint8Array>((resolve) => {
      finishDecrypt = resolve;
    }));
    const pull = new SyncClient(database!, { decrypt }).pull(controller.signal);
    await vi.waitFor(() => expect(decrypt).toHaveBeenCalledOnce());

    controller.abort();
    finishDecrypt!(accountPayload());

    await expect(pull).rejects.toThrow("The sync was cancelled");
    await expect(database!.getRecord(recordId)).resolves.toBeUndefined();
    await expect(database!.getQuarantine(recordId, 1)).resolves.toBeUndefined();
    await expect(database!.getSyncState()).resolves.toBeUndefined();
  });

  it("does not persist a tombstone when cancellation arrives before its mutation", async () => {
    const controller = new AbortController();
    const good = record(1);
    await database!.putRecord(good);
    vi.mocked(axios.get).mockResolvedValue({
      data: {
        records: [wireRecord(2, { tombstone: true })],
        nextCursor: "complete",
        hasMore: false,
      },
    });
    const originalGetRecord = database!.getRecord.bind(database!);
    vi.spyOn(database!, "getRecord").mockImplementation(async (id) => {
      const current = await originalGetRecord(id);
      controller.abort();
      return current;
    });

    await expect(new SyncClient(database!, { decrypt: vi.fn() }).pull(controller.signal)).rejects.toThrow(
      "The sync was cancelled",
    );

    await expect(originalGetRecord(recordId)).resolves.toMatchObject({ revision: 1, tombstone: false });
    await expect(database!.getSyncState()).resolves.toBeUndefined();
  });

  it("keeps decrypted plaintext out of IndexedDB and quarantine metadata", async () => {
    const hiddenText = "cash withdrawal at hidden merchant";
    vi.mocked(axios.get).mockResolvedValue({
      data: { records: [wireRecord(1)], nextCursor: "complete", hasMore: false },
    });

    await new SyncClient(database!, {
      decrypt: vi.fn().mockResolvedValue(new TextEncoder().encode(hiddenText)),
    }).pull();

    const stored = await database!.getRecord(recordId);
    const quarantined = await database!.getQuarantine(recordId, 1);
    expect(JSON.stringify({ stored, quarantined })).not.toContain(hiddenText);
  });
});

describe("sync endpoint client", () => {
  it("turns the server's 409 encrypted record body into a conflict", async () => {
    const response = { status: 409, data: wireRecord(2) };
    vi.mocked(axios.put).mockRejectedValue({ response });
    vi.mocked(axios.isAxiosError).mockReturnValue(true);

    await expect(replaceSyncRecord(recordId, {
      expectedRevision: 1,
      protocolVersion: 1,
      nonce: bytes(1),
      ciphertext: bytes(2),
    })).rejects.toMatchObject({
      record: expect.objectContaining({ id: recordId, revision: 2, ciphertext: bytes(22) }),
    });
  });

  it("uses the six sync routes and sends Base64 wire bytes", async () => {
    vi.mocked(axios.get).mockResolvedValue({ data: { records: [], nextCursor: "", hasMore: false } });
    vi.mocked(axios.post)
      .mockResolvedValueOnce({ data: { records: [] } })
      .mockResolvedValueOnce({
        data: {
          envelope: {
            id: envelopeId,
            groupId,
            wrappedKey: bytes(3),
            nonce: bytes(4),
            encapsulatedKey: bytes(5),
            protocolVersion: 1,
          },
        },
      });
    vi.mocked(axios.put).mockResolvedValue({ data: wireRecord(2) });
    vi.mocked(axios.delete).mockResolvedValue({ data: undefined });

    await getSyncChanges("cursor");
    await createSyncRecords({
      records: [{
        id: recordId,
        idempotencyKey: "create-account",
        recordType: 0,
        parentResourceId: null,
        protocolVersion: 1,
        nonce: bytes(1),
        ciphertext: bytes(2),
        personalEnvelope: {
          wrappedKey: bytes(3),
          nonce: bytes(4),
          protocolVersion: 1,
        },
      }],
    });
    await replaceSyncRecord(recordId, {
      expectedRevision: 1,
      protocolVersion: 1,
      nonce: bytes(1),
      ciphertext: bytes(2),
    });
    await deleteSyncRecord(recordId);
    await addSyncRecordEnvelope(recordId, {
      groupId,
      permission: 0,
      wrappedKey: bytes(3),
      nonce: bytes(4),
      encapsulatedKey: bytes(5),
      protocolVersion: 1,
    });
    await revokeSyncRecordEnvelope(recordId, groupId);

    expect(axios.get).toHaveBeenCalledWith("/api/v1/sync/changes", {
      params: { cursor: "cursor" },
      signal: undefined,
    });
    const createBody = vi.mocked(axios.post).mock.calls[0][1];
    const replaceBody = vi.mocked(axios.put).mock.calls[0][1];
    const envelopeBody = vi.mocked(axios.post).mock.calls[1][1];
    expect(createBody).toEqual({
      records: [expect.objectContaining({
        nonce: "AQID",
        ciphertext: "AgME",
        personalEnvelope: {
          wrappedKey: "AwQF",
          nonce: "BAUG",
          protocolVersion: 1,
        },
      })],
    });
    expect(axios.put).toHaveBeenCalledWith(`/api/v1/sync/records/${recordId}`, {
      expectedRevision: 1,
      protocolVersion: 1,
      nonce: "AQID",
      ciphertext: "AgME",
    });
    expect(axios.delete).toHaveBeenNthCalledWith(1, `/api/v1/sync/records/${recordId}`);
    expect(axios.post).toHaveBeenNthCalledWith(2, `/api/v1/sync/records/${recordId}/envelopes`, {
      groupId,
      permission: 0,
      wrappedKey: "AwQF",
      nonce: "BAUG",
      encapsulatedKey: "BQYH",
      protocolVersion: 1,
    });
    expect(axios.delete).toHaveBeenNthCalledWith(
      2,
      `/api/v1/sync/records/${recordId}/envelopes/${groupId}`,
    );

    const actualAxios = (await vi.importActual<typeof import("axios")>("axios")).default;
    const serializedBodies: string[] = [];
    const adapter: AxiosAdapter = async (configuration) => {
      serializedBodies.push(String(configuration.data));
      return {
        data: {},
        status: 200,
        statusText: "OK",
        headers: {},
        config: configuration,
      };
    };
    for (const body of [createBody, replaceBody, envelopeBody]) {
      await actualAxios.post("/serialization-check", body, { adapter });
    }
    const jsonBodies = serializedBodies.map((body) => JSON.parse(body) as object);
    expect(jsonBodies[0]).toEqual(createBody);
    expect(jsonBodies[1]).toEqual(replaceBody);
    expect(jsonBodies[2]).toEqual(envelopeBody);
  });
});
