import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ConflictManager } from "./conflicts";
import { OutboxManager, type OutboxCipher } from "./outbox";
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
const bytes = (seed: number): Uint8Array => new Uint8Array([seed, seed + 1, seed + 2]);

const record = (revision: number, ciphertextSeed: number): VaultRecord => ({
  id: recordId,
  recordType: "account",
  ownerId,
  parentResourceId: null,
  revision,
  protocolVersion: 1,
  nonce: bytes(ciphertextSeed),
  ciphertext: bytes(ciphertextSeed + 10),
  envelopes: [{
    id: "55555555-5555-4555-8555-555555555555",
    groupId: null,
    wrappedKey: bytes(30),
    nonce: bytes(40),
    encapsulatedKey: null,
    protocolVersion: 1,
  }],
  tombstone: false,
  sequenceNumber: revision,
  serverCreatedAt: "2026-08-09T10:00:00Z",
  serverUpdatedAt: "2026-08-09T10:00:00Z",
});

const syncRecord = (revision: number, ciphertextSeed: number): SyncRecord => ({
  ...record(revision, ciphertextSeed),
  envelopes: record(revision, ciphertextSeed).envelopes,
});

const replaceMutation = (revision = 1): VaultOutboxMutation => ({
  kind: "replace",
  record: record(revision, 20),
  request: {
    expectedRevision: revision,
    protocolVersion: 1,
    nonce: bytes(21),
    ciphertext: bytes(22),
  },
});

const cipher = (replacement = replaceMutation(2)): OutboxCipher => ({
  encrypt: vi.fn().mockResolvedValue(replacement),
  decrypt: vi.fn(async (value: VaultRecord) =>
    new TextEncoder().encode(value.ciphertext[0] === 30 ? localText : serverText)),
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
  it("keeps both decrypted revisions only in unlocked memory after a 409 and preserves the local record", async () => {
    vi.stubGlobal("crypto", { ...crypto, randomUUID: vi.fn()
      .mockReturnValueOnce(operationId).mockReturnValueOnce(idempotencyKey) });
    const conflicts = new ConflictManager();
    const localMutation = replaceMutation(1);
    const localCipher = cipher(localMutation);
    const manager = new OutboxManager(database!, localCipher, { apply: vi.fn() }, conflicts, {
      create: vi.fn(),
      replace: vi.fn().mockRejectedValue(new SyncConflictError(syncRecord(2, 90))),
      remove: vi.fn(),
    });
    await manager.queue({ kind: "replace", recordId, expectedRevision: 1, plaintext: new Uint8Array([1]) });
    await database!.putRecord(record(2, 90));

    await manager.replay();

    expect(conflicts.get(recordId)).toMatchObject({
      recordId,
      latest: { revision: 2 },
      outboxEntry: { operationId, idempotencyKey },
    });
    expect(new TextDecoder().decode(conflicts.get(recordId)!.mine)).toBe(localText);
    expect(new TextDecoder().decode(conflicts.get(recordId)!.theirs)).toBe(serverText);
    await expect(database!.getRecord(recordId)).resolves.toMatchObject({
      revision: 1,
      ciphertext: expect.anything(),
    });
    expect(JSON.stringify(await database!.outboxEntries())).not.toContain(localText);
    expect(JSON.stringify(await database!.outboxEntries())).not.toContain(serverText);
    await expect(database!.outboxEntries()).resolves.toEqual([
      expect.objectContaining({ latestServerRecord: expect.objectContaining({ revision: 2 }) }),
    ]);
  });

  it("keep mine re-encrypts against the latest revision and retries the queued replace", async () => {
    vi.stubGlobal("crypto", { ...crypto, randomUUID: vi.fn()
      .mockReturnValueOnce(operationId).mockReturnValueOnce(idempotencyKey) });
    const conflicts = new ConflictManager();
    const reencrypted = replaceMutation(2);
    const localCipher = cipher(reencrypted);
    const api = {
      create: vi.fn(),
      replace: vi.fn()
        .mockRejectedValueOnce(new SyncConflictError(syncRecord(2, 90)))
        .mockResolvedValue(syncRecord(3, 100)),
      remove: vi.fn(),
    };
    const manager = new OutboxManager(database!, localCipher, { apply: vi.fn() }, conflicts, api);
    await manager.queue({ kind: "replace", recordId, expectedRevision: 1, plaintext: new Uint8Array([1]) });
    await manager.replay();

    await manager.keepMine(recordId);

    expect(localCipher.encrypt).toHaveBeenLastCalledWith(expect.objectContaining({
      kind: "replace",
      recordId,
      expectedRevision: 2,
      plaintext: expect.anything(),
    }));
    expect(api.replace).toHaveBeenLastCalledWith(recordId, expect.objectContaining({ expectedRevision: 2 }), undefined);
    expect(conflicts.get(recordId)).toBeUndefined();
    await expect(database!.outboxEntries()).resolves.toEqual([]);
    await expect(database!.getRecord(recordId)).resolves.toMatchObject({ revision: 3 });
  });

  it("keep theirs drops the queued mutation and applies the latest encrypted record", async () => {
    vi.stubGlobal("crypto", { ...crypto, randomUUID: vi.fn()
      .mockReturnValueOnce(operationId).mockReturnValueOnce(idempotencyKey) });
    const projection = { apply: vi.fn() };
    const conflicts = new ConflictManager();
    const manager = new OutboxManager(database!, cipher(), projection, conflicts, {
      create: vi.fn(),
      replace: vi.fn().mockRejectedValue(new SyncConflictError(syncRecord(2, 90))),
      remove: vi.fn(),
    });
    await manager.queue({ kind: "replace", recordId, expectedRevision: 1, plaintext: new Uint8Array([1]) });
    await manager.replay();

    await manager.keepTheirs(recordId);

    expect(conflicts.get(recordId)).toBeUndefined();
    await expect(database!.outboxEntries()).resolves.toEqual([]);
    const stored = await database!.getRecord(recordId);
    expect(stored).toMatchObject({ revision: 2 });
    expect(Array.from(stored!.ciphertext)).toEqual(Array.from(bytes(100)));
    const lastProjectionCall = projection.apply.mock.calls[projection.apply.mock.calls.length - 1];
    expect(lastProjectionCall[0]).toMatchObject({ revision: 2 });
    expect(ArrayBuffer.isView(lastProjectionCall[1]!)).toBe(true);
  });
});
