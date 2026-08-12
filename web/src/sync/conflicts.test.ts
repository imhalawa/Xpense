import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ConflictManager } from "./conflicts";
import { OutboxManager, type OutboxMutationBuilder } from "./outbox";
import { SyncConflictError, type SyncRecord } from "./syncClient";
import {
  deleteVaultDatabase,
  openVaultDatabase,
  type VaultDatabase,
  type VaultOutboxMutation,
  type VaultRecord,
} from "../vault/vaultDatabase";

const ownerId = "11111111-1111-4111-8111-111111111111";
const recordId = "22222222-2222-4222-8222-222222222222";
const operationId = "33333333-3333-4333-8333-433333333333";
const idempotencyKey = "44444444-4444-4444-8444-444444444444";
const localText = "my private changed account label";
const serverText = "their private changed account label";
const text = (value: string): Uint8Array => new TextEncoder().encode(value);

const record = (revision: number, label: string): VaultRecord => ({
  id: recordId,
  recordType: "account",
  ownerId,
  parentResourceId: null,
  revision,
  payload: text(label),
  tombstone: false,
  sequenceNumber: revision,
  serverCreatedAt: "2026-08-09T10:00:00Z",
  serverUpdatedAt: "2026-08-09T10:00:00Z",
});

const syncRecord = (revision: number, label: string): SyncRecord => record(revision, label);

const replaceMutation = (revision = 1): VaultOutboxMutation => ({
  kind: "replace",
  record: record(revision, localText),
  request: { expectedRevision: revision, payload: text(localText) },
});

const builder = (replacement = replaceMutation(2)): OutboxMutationBuilder => ({
  build: vi.fn().mockResolvedValue(replacement),
});

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

describe("ConflictManager", () => {
  it("replaces a same-record conflict with the newer revision", () => {
    const conflicts = new ConflictManager();
    const entry = { operationId, idempotencyKey, sequence: 1, mutation: replaceMutation(1) };
    conflicts.add({ recordId, outboxEntry: entry, local: record(1, localText), latest: record(2, serverText), mine: text(localText), theirs: text(serverText) });

    conflicts.add({ recordId, outboxEntry: entry, local: record(1, localText), latest: record(3, serverText), mine: text(localText), theirs: text(serverText) });

    expect(conflicts.entries()).toHaveLength(1);
    expect(conflicts.get(recordId)).toMatchObject({ latest: { revision: 3 } });
  });

  it("keeps both revisions in memory after a 409 and preserves the local record", async () => {
    vi.stubGlobal("crypto", { ...crypto, randomUUID: vi.fn()
      .mockReturnValueOnce(operationId).mockReturnValueOnce(idempotencyKey) });
    const conflicts = new ConflictManager();
    const manager = new OutboxManager(database!, builder(replaceMutation(1)), { apply: vi.fn() }, conflicts, {
      create: vi.fn(),
      replace: vi.fn().mockRejectedValue(new SyncConflictError(syncRecord(2, serverText))),
      remove: vi.fn(),
    });
    await manager.queue({ kind: "replace", recordId, expectedRevision: 1, payload: text(localText) });
    await database!.putRecord(record(2, serverText));

    await manager.replay();

    expect(conflicts.get(recordId)).toMatchObject({
      recordId,
      latest: { revision: 2 },
      outboxEntry: { operationId, idempotencyKey },
    });
    expect(new TextDecoder().decode(conflicts.get(recordId)!.mine)).toBe(localText);
    expect(new TextDecoder().decode(conflicts.get(recordId)!.theirs)).toBe(serverText);
    await expect(database!.getRecord(recordId)).resolves.toMatchObject({ revision: 1 });
    await expect(database!.outboxEntries()).resolves.toEqual([
      expect.objectContaining({ latestServerRecord: expect.objectContaining({ revision: 2 }) }),
    ]);
  });

  it("keep mine rebuilds against the latest revision and retries the queued replace", async () => {
    vi.stubGlobal("crypto", { ...crypto, randomUUID: vi.fn()
      .mockReturnValueOnce(operationId).mockReturnValueOnce(idempotencyKey) });
    const conflicts = new ConflictManager();
    const mutations = builder(replaceMutation(2));
    const api = {
      create: vi.fn(),
      replace: vi.fn()
        .mockRejectedValueOnce(new SyncConflictError(syncRecord(2, serverText)))
        .mockResolvedValue(syncRecord(3, localText)),
      remove: vi.fn(),
    };
    const manager = new OutboxManager(database!, mutations, { apply: vi.fn() }, conflicts, api);
    await manager.queue({ kind: "replace", recordId, expectedRevision: 1, payload: text(localText) });
    await manager.replay();

    await manager.keepMine(recordId);

    expect(mutations.build).toHaveBeenLastCalledWith(expect.objectContaining({
      kind: "replace",
      recordId,
      expectedRevision: 2,
      payload: expect.anything(),
    }));
    expect(api.replace).toHaveBeenLastCalledWith(recordId, expect.objectContaining({ expectedRevision: 2 }), undefined);
    expect(conflicts.get(recordId)).toBeUndefined();
    await expect(database!.outboxEntries()).resolves.toEqual([]);
    await expect(database!.getRecord(recordId)).resolves.toMatchObject({ revision: 3 });
  });

  it("keep theirs drops the queued mutation and applies the latest record", async () => {
    vi.stubGlobal("crypto", { ...crypto, randomUUID: vi.fn()
      .mockReturnValueOnce(operationId).mockReturnValueOnce(idempotencyKey) });
    const projection = { apply: vi.fn() };
    const conflicts = new ConflictManager();
    const manager = new OutboxManager(database!, builder(), projection, conflicts, {
      create: vi.fn(),
      replace: vi.fn().mockRejectedValue(new SyncConflictError(syncRecord(2, serverText))),
      remove: vi.fn(),
    });
    await manager.queue({ kind: "replace", recordId, expectedRevision: 1, payload: text(localText) });
    await manager.replay();

    await manager.keepTheirs(recordId);

    expect(conflicts.get(recordId)).toBeUndefined();
    await expect(database!.outboxEntries()).resolves.toEqual([]);
    const stored = await database!.getRecord(recordId);
    expect(stored).toMatchObject({ revision: 2 });
    expect(new TextDecoder().decode(stored!.payload)).toBe(serverText);
    const lastProjectionCall = projection.apply.mock.calls[projection.apply.mock.calls.length - 1];
    expect(lastProjectionCall[0]).toMatchObject({ revision: 2 });
    expect(new TextDecoder().decode(lastProjectionCall[1] as Uint8Array)).toBe(serverText);
  });
});
