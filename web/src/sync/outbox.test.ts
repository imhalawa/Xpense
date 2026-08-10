import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import axios from "axios";
import { ConflictManager } from "./conflicts";
import {
  OutboxManager,
  type OutboxCipher,
  type OutboxEncryptionRequest,
  type SyncMutationApi,
} from "./outbox";
import type { SyncRecord } from "./syncClient";
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
const operationId = "33333333-3333-4333-8333-333333333333";
const idempotencyKey = "44444444-4444-4444-8444-444444444444";
const hiddenText = "cash withdrawal at a private merchant";
const bytes = (seed: number): Uint8Array => new Uint8Array([seed, seed + 1, seed + 2]);

const record = (revision = 1): VaultRecord => ({
  id: recordId,
  recordType: "account",
  ownerId,
  parentResourceId: null,
  revision,
  protocolVersion: 1,
  nonce: bytes(10 + revision),
  ciphertext: bytes(20 + revision),
  envelopes: [{
    id: "55555555-5555-4555-8555-555555555555",
    groupId: null,
    wrappedKey: bytes(30 + revision),
    nonce: bytes(40 + revision),
    encapsulatedKey: null,
    protocolVersion: 1,
  }],
  tombstone: false,
  sequenceNumber: revision,
  serverCreatedAt: "2026-08-09T10:00:00Z",
  serverUpdatedAt: "2026-08-09T10:00:00Z",
});

const createMutation = (revision = 1): VaultOutboxMutation => ({
  kind: "create",
  record: record(revision),
  request: {
    id: recordId,
    recordType: 0,
    parentResourceId: null,
    protocolVersion: 1,
    nonce: bytes(11),
    ciphertext: bytes(12),
    personalEnvelope: {
      wrappedKey: bytes(13),
      nonce: bytes(14),
      protocolVersion: 1,
    },
  },
});

const wireRecord = (revision = 1) => {
  const value = record(revision);
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

const cipher = (mutation = createMutation()): OutboxCipher => ({
  encrypt: vi.fn(async (_request: OutboxEncryptionRequest) => mutation),
  decrypt: vi.fn(async () => new TextEncoder().encode(hiddenText)),
});

let database: VaultDatabase | undefined;

beforeEach(async () => {
  vi.mocked(axios.post).mockReset();
  await deleteVaultDatabase();
  database = await openVaultDatabase();
});

afterEach(async () => {
  database?.close();
  database = undefined;
  await deleteVaultDatabase();
});

describe("OutboxManager", () => {
  it("queues an offline encrypted create and replays it with the original idempotency key", async () => {
    vi.stubGlobal("crypto", { ...crypto, randomUUID: vi.fn()
      .mockReturnValueOnce(operationId)
      .mockReturnValueOnce(idempotencyKey) });
    const projection = { apply: vi.fn() };
    const manager = new OutboxManager(database!, cipher(), projection, new ConflictManager());

    await manager.queue({
      kind: "create",
      recordId,
      plaintext: new TextEncoder().encode(hiddenText),
    });

    expect(axios.post).not.toHaveBeenCalled();
    expect(projection.apply.mock.calls[0][0]).toMatchObject({ id: recordId });
    expect(ArrayBuffer.isView(projection.apply.mock.calls[0][1]!)).toBe(true);
    expect(JSON.stringify(await database!.outboxEntries())).not.toContain(hiddenText);

    vi.mocked(axios.post).mockResolvedValue({ data: { records: [wireRecord()] } });
    await manager.replay();

    expect(axios.post).toHaveBeenCalledWith("/api/v1/sync/records", {
      records: [expect.objectContaining({
        idempotencyKey,
        nonce: "CwwN",
        ciphertext: "DA0O",
      })],
    });
    await expect(database!.outboxEntries()).resolves.toEqual([]);
  });

  it("does not send a replayed create twice after its acknowledgement is persisted", async () => {
    vi.stubGlobal("crypto", { ...crypto, randomUUID: vi.fn()
      .mockReturnValueOnce(operationId)
      .mockReturnValueOnce(idempotencyKey) });
    const manager = new OutboxManager(database!, cipher(), { apply: vi.fn() }, new ConflictManager());
    await manager.queue({ kind: "create", recordId, plaintext: new Uint8Array([1]) });
    vi.mocked(axios.post).mockResolvedValue({ data: { records: [wireRecord()] } });

    await manager.replay();
    await manager.replay();

    expect(axios.post).toHaveBeenCalledTimes(1);
    await expect(database!.records()).resolves.toHaveLength(1);
  });

  it("replays the exact idempotency key after a committed response is dropped and a reload", async () => {
    vi.stubGlobal("crypto", { ...crypto, randomUUID: vi.fn()
      .mockReturnValueOnce(operationId).mockReturnValueOnce(idempotencyKey) });
    let committed: SyncRecord | undefined;
    let commitCount = 0;
    const receivedKeys: string[] = [];
    const api: SyncMutationApi = {
      create: vi.fn(async (request) => {
        receivedKeys.push(request.records[0]!.idempotencyKey);
        if (committed === undefined) {
          committed = record();
          commitCount += 1;
          throw new Error("The committed response was dropped");
        }
        return [committed];
      }),
      replace: vi.fn(),
      remove: vi.fn(),
    };
    const firstManager = new OutboxManager(
      database!,
      cipher(),
      { apply: vi.fn() },
      new ConflictManager(),
      api,
    );
    await firstManager.queue({ kind: "create", recordId, plaintext: new Uint8Array([1]) });

    await firstManager.replay();
    await expect(database!.outboxEntries()).resolves.toHaveLength(1);
    database!.close();
    database = await openVaultDatabase();

    const reloadedManager = new OutboxManager(
      database!,
      cipher(),
      { apply: vi.fn() },
      new ConflictManager(),
      api,
    );
    await reloadedManager.replay();

    expect(receivedKeys).toEqual([idempotencyKey, idempotencyKey]);
    expect(commitCount).toBe(1);
    await expect(database!.outboxEntries()).resolves.toEqual([]);
    await expect(database!.records()).resolves.toHaveLength(1);
  });

  it("keeps the encrypted queue across lock and reload without persisting plaintext", async () => {
    vi.stubGlobal("crypto", { ...crypto, randomUUID: vi.fn()
      .mockReturnValueOnce(operationId)
      .mockReturnValueOnce(idempotencyKey) });
    const manager = new OutboxManager(database!, cipher(), { apply: vi.fn() }, new ConflictManager());
    await manager.queue({
      kind: "create",
      recordId,
      plaintext: new TextEncoder().encode(hiddenText),
    });
    database!.close();
    database = await openVaultDatabase();

    const queued = await database.outboxEntries();
    expect(queued).toHaveLength(1);
    expect(queued[0]).toMatchObject({ operationId, idempotencyKey, mutation: { kind: "create" } });
    expect(JSON.stringify(queued)).not.toContain(hiddenText);
    expect(JSON.stringify(await database.get("outbox", operationId))).not.toContain(hiddenText);
  });

  it("does not start a later operation when an earlier replay fails", async () => {
    const first = cipher(createMutation());
    const secondMutation = createMutation();
    if (secondMutation.kind !== "create") throw new Error("The fixture must create a record");
    secondMutation.request.id = "66666666-6666-4666-8666-666666666666";
    secondMutation.record = { ...record(), id: secondMutation.request.id };
    const manager = new OutboxManager(database!, first, { apply: vi.fn() }, new ConflictManager(), {
      create: vi.fn().mockRejectedValue(new Error("offline")),
      replace: vi.fn(),
      remove: vi.fn(),
    });
    vi.stubGlobal("crypto", { ...crypto, randomUUID: vi.fn()
      .mockReturnValueOnce(operationId).mockReturnValueOnce(idempotencyKey)
      .mockReturnValueOnce("77777777-7777-4777-8777-777777777777")
      .mockReturnValueOnce("88888888-8888-4888-8888-888888888888") });
    await manager.queue({ kind: "create", recordId, plaintext: new Uint8Array([1]) });
    vi.mocked(first.encrypt).mockResolvedValueOnce(secondMutation);
    await manager.queue({ kind: "create", recordId: secondMutation.record.id, plaintext: new Uint8Array([2]) });

    await manager.replay();

    expect(manager.syncApi.create).toHaveBeenCalledTimes(1);
    await expect(database!.outboxEntries()).resolves.toHaveLength(2);
  });

  it("leaves the queue unchanged when replay is cancelled before it starts", async () => {
    vi.stubGlobal("crypto", { ...crypto, randomUUID: vi.fn()
      .mockReturnValueOnce(operationId).mockReturnValueOnce(idempotencyKey) });
    const api = { create: vi.fn(), replace: vi.fn(), remove: vi.fn() };
    const manager = new OutboxManager(database!, cipher(), { apply: vi.fn() }, new ConflictManager(), api);
    await manager.queue({ kind: "create", recordId, plaintext: new Uint8Array([1]) });
    const controller = new AbortController();
    controller.abort();

    await expect(manager.replay(controller.signal)).rejects.toMatchObject({
      name: "AbortError",
      message: "The sync was cancelled",
    });

    expect(api.create).not.toHaveBeenCalled();
    await expect(database!.outboxEntries()).resolves.toHaveLength(1);
  });

  it("propagates an in-flight AbortError and leaves the durable queue unchanged", async () => {
    vi.stubGlobal("crypto", { ...crypto, randomUUID: vi.fn()
      .mockReturnValueOnce(operationId).mockReturnValueOnce(idempotencyKey) });
    let requestStarted: (() => void) | undefined;
    const started = new Promise<void>((resolve) => {
      requestStarted = resolve;
    });
    const api: SyncMutationApi = {
      create: vi.fn((_request, signal) => new Promise<SyncRecord[]>((_resolve, reject) => {
        requestStarted?.();
        signal?.addEventListener("abort", () => {
          reject(new DOMException("The request was aborted", "AbortError"));
        }, { once: true });
      })),
      replace: vi.fn(),
      remove: vi.fn(),
    };
    const manager = new OutboxManager(
      database!,
      cipher(),
      { apply: vi.fn() },
      new ConflictManager(),
      api,
    );
    await manager.queue({ kind: "create", recordId, plaintext: new Uint8Array([1]) });
    const controller = new AbortController();
    const replay = manager.replay(controller.signal);
    await started;

    controller.abort();

    await expect(replay).rejects.toMatchObject({ name: "AbortError" });
    await expect(database!.outboxEntries()).resolves.toHaveLength(1);
  });

  it("shares one in-progress replay so queued operations retain their order", async () => {
    vi.stubGlobal("crypto", { ...crypto, randomUUID: vi.fn()
      .mockReturnValueOnce(operationId).mockReturnValueOnce(idempotencyKey) });
    let acknowledge: ((records: SyncRecord[]) => void) | undefined;
    const api: SyncMutationApi = {
      create: vi.fn(() => new Promise<SyncRecord[]>((resolve) => { acknowledge = resolve; })),
      replace: vi.fn(),
      remove: vi.fn(),
    };
    const manager = new OutboxManager(database!, cipher(), { apply: vi.fn() }, new ConflictManager(), api);
    await manager.queue({ kind: "create", recordId, plaintext: new Uint8Array([1]) });

    const firstReplay = manager.replay();
    const secondReplay = manager.replay();
    expect(secondReplay).toBe(firstReplay);
    await vi.waitFor(() => expect(api.create).toHaveBeenCalledOnce());
    acknowledge!([record()]);
    await firstReplay;

    expect(api.create).toHaveBeenCalledTimes(1);
  });
});
