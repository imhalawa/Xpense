import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import axios, { type AxiosAdapter } from "axios";
import {
  SyncClient,
  createSyncRecords,
  deleteSyncRecord,
  getSyncChanges,
  replaceSyncRecord,
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
const bytes = (seed: number): Uint8Array => new Uint8Array([seed, seed + 1, seed + 2]);
const accountPayload = (label = "Cash"): Uint8Array =>
  new TextEncoder().encode(JSON.stringify({
    schemaVersion: 1,
    recordId,
    createdAt: "2026-08-09T10:00:00Z",
    updatedAt: null,
    label,
    balance: { minorUnits: 1250, currency: "EUR" },
    openingBalanceMinorUnits: 1250,
    currency: "EUR",
    isDefault: true,
  }));

const record = (revision: number, overrides: Partial<SyncRecord> = {}): SyncRecord => ({
  id: recordId,
  recordType: "account",
  ownerId,
  parentResourceId: null,
  revision,
  payload: accountPayload(),
  tombstone: false,
  sequenceNumber: revision,
  serverCreatedAt: "2026-08-09T10:00:00Z",
  serverUpdatedAt: "2026-08-09T10:00:00Z",
  ...overrides,
});

const base64 = (value: Uint8Array): string =>
  btoa(Array.from(value, (byte) => String.fromCharCode(byte)).join(""));

const wireRecord = (revision: number, overrides: Partial<SyncRecord> = {}) => {
  const value = record(revision, overrides);
  return { ...value, recordType: 0, payload: base64(value.payload) };
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
  it("stores a pulled payload the projection can decode", async () => {
    vi.mocked(axios.get).mockResolvedValue({
      data: { records: [wireRecord(1)], nextCursor: "complete", hasMore: false },
    });

    await new SyncClient(database!).pull();

    const stored = await database!.getRecord(recordId);
    expect(new TextDecoder().decode(stored!.payload)).toContain("Cash");
    await expect(database!.getSyncState()).resolves.toEqual({ key: "changes", cursor: "complete" });
  });

  it("stages a pulled server revision without overwriting a queued optimistic payload", async () => {
    const optimistic: VaultRecord = record(1, { payload: accountPayload("Queued") });
    const mutation: VaultOutboxMutation = {
      kind: "replace",
      record: optimistic,
      request: { expectedRevision: 1, payload: optimistic.payload },
    };
    await database!.enqueueOutbox({
      operationId: "55555555-5555-4555-8555-555555555555",
      idempotencyKey: "66666666-6666-4666-8666-666666666666",
      mutation,
    });
    vi.mocked(axios.get).mockResolvedValue({
      data: { records: [wireRecord(2)], nextCursor: "staged", hasMore: false },
    });

    await new SyncClient(database!).pull();

    const local = await database!.getRecord(recordId);
    expect(local).toMatchObject({ revision: 1 });
    expect(new TextDecoder().decode(local!.payload)).toContain("Queued");
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
        data: { records: [wireRecord(1)], nextCursor: "first", hasMore: true },
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

    await new SyncClient(database!).pull();

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
  });

  it("resumes from the persisted cursor after a reload", async () => {
    await database!.putSyncState({ key: "changes", cursor: "saved" });
    database!.close();
    database = await openVaultDatabase();
    vi.mocked(axios.get).mockResolvedValue({
      data: { records: [], nextCursor: "saved", hasMore: false },
    });

    await new SyncClient(database!).pull();

    expect(axios.get).toHaveBeenCalledWith("/api/v1/sync/changes", {
      params: { cursor: "saved" },
      signal: undefined,
    });
  });

  it("quarantines an unparsable revision without replacing the last good record", async () => {
    await database!.putRecord(record(1));
    vi.mocked(axios.get).mockResolvedValue({
      data: {
        records: [wireRecord(2, { payload: new TextEncoder().encode('{"schemaVersion":1}') })],
        nextCursor: "after-bad",
        hasMore: false,
      },
    });

    await new SyncClient(database!).pull();

    await expect(database!.getRecord(recordId)).resolves.toMatchObject({ revision: 1 });
    await expect(database!.getQuarantine(recordId, 2)).resolves.toMatchObject({
      recordId,
      revision: 2,
      reason: "invalid-payload",
      record: { revision: 2 },
    });
    await expect(database!.getSyncState()).resolves.toEqual({ key: "changes", cursor: "after-bad" });
  });

  it("does not make progress when cancellation is already requested", async () => {
    const controller = new AbortController();
    controller.abort();

    await expect(new SyncClient(database!).pull(controller.signal)).rejects.toThrow(
      "The sync was cancelled",
    );

    expect(axios.get).not.toHaveBeenCalled();
    await expect(database!.getSyncState()).resolves.toBeUndefined();
  });

  it("does not persist a tombstone when cancellation arrives before its mutation", async () => {
    const controller = new AbortController();
    await database!.putRecord(record(1));
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

    await expect(new SyncClient(database!).pull(controller.signal)).rejects.toThrow(
      "The sync was cancelled",
    );

    await expect(originalGetRecord(recordId)).resolves.toMatchObject({ revision: 1, tombstone: false });
    await expect(database!.getSyncState()).resolves.toBeUndefined();
  });
});

describe("sync endpoint client", () => {
  it("turns the server's 409 record body into a conflict", async () => {
    vi.mocked(axios.put).mockRejectedValue({ response: { status: 409, data: wireRecord(2) } });
    vi.mocked(axios.isAxiosError).mockReturnValue(true);

    await expect(replaceSyncRecord(recordId, {
      expectedRevision: 1,
      payload: bytes(1),
    })).rejects.toMatchObject({
      record: expect.objectContaining({ id: recordId, revision: 2 }),
    });
  });

  it("uses the four sync routes and sends Base64 payload bytes", async () => {
    vi.mocked(axios.get).mockResolvedValue({ data: { records: [], nextCursor: "", hasMore: false } });
    vi.mocked(axios.post).mockResolvedValue({ data: { records: [] } });
    vi.mocked(axios.put).mockResolvedValue({ data: wireRecord(2) });
    vi.mocked(axios.delete).mockResolvedValue({ data: undefined });

    await getSyncChanges("cursor");
    await createSyncRecords({
      records: [{
        id: recordId,
        idempotencyKey: "create-account",
        recordType: 0,
        parentResourceId: null,
        payload: bytes(1),
      }],
    });
    await replaceSyncRecord(recordId, { expectedRevision: 1, payload: bytes(2) });
    await deleteSyncRecord(recordId);

    expect(axios.get).toHaveBeenCalledWith("/api/v1/sync/changes", {
      params: { cursor: "cursor" },
      signal: undefined,
    });
    const createBody = vi.mocked(axios.post).mock.calls[0][1];
    const replaceBody = vi.mocked(axios.put).mock.calls[0][1];
    expect(createBody).toEqual({
      records: [{
        id: recordId,
        idempotencyKey: "create-account",
        recordType: 0,
        parentResourceId: null,
        payload: "AQID",
      }],
    });
    expect(axios.put).toHaveBeenCalledWith(`/api/v1/sync/records/${recordId}`, {
      expectedRevision: 1,
      payload: "AgME",
    });
    expect(axios.delete).toHaveBeenCalledWith(`/api/v1/sync/records/${recordId}`);

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
    for (const body of [createBody, replaceBody]) {
      await actualAxios.post("/serialization-check", body, { adapter });
    }
    const jsonBodies = serializedBodies.map((body) => JSON.parse(body) as object);
    expect(jsonBodies[0]).toEqual(createBody);
    expect(jsonBodies[1]).toEqual(replaceBody);
  });
});
