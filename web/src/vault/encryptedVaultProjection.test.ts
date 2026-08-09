import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { SyncLifecycleCoordinator } from "../sync/lifecycle";
import type { SyncMutationApi } from "../sync/outbox";
import type { SyncRecord } from "../sync/syncClient";
import { Currency } from "../typings/enums/Currency";
import { VaultWorkerUnavailableError } from "../crypto/worker/vaultWorkerClient";
import { encodeVaultPayloadV1, type VaultPayloadV1 } from "./payloadV1";
import type { ProjectionCryptoBridge } from "./transitionVaultProjection";
import { deleteVaultDatabase, openVaultDatabase, type VaultDatabase, type VaultRecord } from "./vaultDatabase";
import { encryptedVaultProjection } from "./encryptedVaultProjection";

const ownerId = "11111111-1111-4111-8111-111111111111";
const accountId = "22222222-2222-4222-8222-222222222222";
const scaleId = "33333333-3333-4333-8333-333333333333";
const categoryId = "44444444-4444-4444-8444-444444444444";
const transactionId = "55555555-5555-4555-8555-555555555555";
const createdAccountId = "66666666-6666-4666-8666-666666666666";
const essentialScaleId = "88888888-8888-4888-8888-888888888888";
const malformedId = "99999999-9999-4999-8999-999999999999";
const budgetId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const corruptId = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
const instant = "2026-08-09T10:00:00.000Z";
const bytes = (value: number) => new Uint8Array([value, value + 1, value + 2]);
const envelope = { id: "77777777-7777-4777-8777-777777777777", groupId: null, wrappedKey: bytes(1), nonce: bytes(4), encapsulatedKey: null, protocolVersion: 1 };
const payload = (recordId: string, fields: Record<string, unknown>): VaultPayloadV1 => ({ schemaVersion: 1, recordId, createdAt: instant, updatedAt: null, ...fields });
const stored = (id: string, recordType: VaultRecord["recordType"], parentResourceId: string | null): VaultRecord => ({ id, recordType, ownerId, parentResourceId, revision: 1, protocolVersion: 1, nonce: bytes(7), ciphertext: bytes(10), envelopes: [{ ...envelope, id }], tombstone: false, sequenceNumber: 1, serverCreatedAt: instant, serverUpdatedAt: instant });

let database: VaultDatabase;
let plaintext = new Map<string, Uint8Array>();

beforeEach(async () => {
  await deleteVaultDatabase();
  database = await openVaultDatabase();
  plaintext = new Map([
    [accountId, encodeVaultPayloadV1(payload(accountId, { label: "Wallet", balance: { minorUnits: 500, currency: "EUR" }, openingBalanceMinorUnits: 1000, currency: "EUR", isDefault: true }))],
    [scaleId, encodeVaultPayloadV1(payload(scaleId, { label: "Useful", weight: 2 }))],
    [essentialScaleId, encodeVaultPayloadV1(payload(essentialScaleId, { label: "Essential", weight: 5 }))],
    [categoryId, encodeVaultPayloadV1(payload(categoryId, { label: "Coffee", necessityScaleId: scaleId, priority: { label: "Useful" } }))],
    [transactionId, encodeVaultPayloadV1(payload(transactionId, { kind: "expense", amount: { minorUnits: 500, currency: "EUR" }, sourceAccountId: accountId, destinationAccountId: null, categoryRecordId: categoryId, merchantRecordId: null, tagRecordIds: [], reason: null, occurredAt: instant }))],
    [budgetId, encodeVaultPayloadV1(payload(budgetId, { categoryRecordId: categoryId, amount: { minorUnits: 900, currency: "EUR" }, recurrence: "Monthly", startsOn: "2026-08-01", endsOn: null, alertThresholdPercent: null }))],
  ]);
  await database.putRecord(stored(accountId, "account", accountId));
  await database.putRecord(stored(scaleId, "necessityScale", null));
  await database.putRecord(stored(essentialScaleId, "necessityScale", null));
  await database.putRecord(stored(categoryId, "category", null));
  await database.putRecord(stored(transactionId, "transaction", accountId));
  await database.putRecord(stored(budgetId, "budget", budgetId));
  database.close();
});

afterEach(async () => {
  database?.close();
  await deleteVaultDatabase();
});

const syncRecord = (request: Parameters<SyncMutationApi["create"]>[0]["records"][number]): SyncRecord => ({
  id: request.id,
  recordType: "account",
  ownerId,
  parentResourceId: request.parentResourceId,
  revision: 1,
  protocolVersion: 1,
  nonce: request.nonce,
  ciphertext: request.ciphertext,
  envelopes: [{ ...envelope, id: request.id, wrappedKey: request.personalEnvelope.wrappedKey, nonce: request.personalEnvelope.nonce }],
  tombstone: false,
  sequenceNumber: 2,
  serverCreatedAt: instant,
  serverUpdatedAt: instant,
});

describe("encrypted vault projection", () => {
  it("rebuilds UUID-native balances from IndexedDB without a legacy client", async () => {
    const bridge: ProjectionCryptoBridge = {
      ownerId,
      decrypt: vi.fn(async (record) => new Uint8Array(plaintext.get(record.id)!)),
      encryptNew: vi.fn(),
      encryptReplacement: vi.fn(),
    };
    const projection = encryptedVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn() });
    projection.attachCrypto(bridge);

    await projection.unlock();

    await expect(projection.listAccounts("personal")).resolves.toMatchObject([{ id: accountId, openingBalanceMinorUnits: 1000 }]);
    await expect(projection.listAccountBalances("personal")).resolves.toEqual({ state: "available", balances: [{ currency: Currency.EUR, minorUnits: 500 }] });
    await expect(projection.listTransactions("personal")).resolves.toMatchObject([{ id: transactionId, accountId, categoryId }]);
    projection.lock();
  });

  it("quarantines an authenticated malformed payload and still renders good records", async () => {
    database = await openVaultDatabase();
    await database.putRecord(stored(malformedId, "merchant", null));
    database.close();
    plaintext.set(malformedId, new TextEncoder().encode('{"schemaVersion":1}'));
    const projection = encryptedVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn() });
    projection.attachCrypto({
      ownerId,
      decrypt: vi.fn(async (record) => new Uint8Array(plaintext.get(record.id)!)),
      encryptNew: vi.fn(),
      encryptReplacement: vi.fn(),
    });

    await projection.unlock();

    await expect(projection.listAccounts("personal")).resolves.toContainEqual(expect.objectContaining({ id: accountId }));
    database = await openVaultDatabase();
    await expect(database.getQuarantine(malformedId, 1)).resolves.toMatchObject({ reason: "invalid-payload" });
    database.close();
    projection.lock();
  });

  it("never decrypts a record covered by a legacy record-wide quarantine sentinel", async () => {
    database = await openVaultDatabase();
    await database.putRecord(stored(malformedId, "merchant", null));
    await database.putQuarantine({ recordId: malformedId, revision: 0, reason: "authentication-failed" });
    database.close();
    const decrypt = vi.fn(async (record: VaultRecord) => new Uint8Array(plaintext.get(record.id)!));
    const projection = encryptedVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn() });
    projection.attachCrypto({ ownerId, decrypt, encryptNew: vi.fn(), encryptReplacement: vi.fn() });

    await projection.unlock();

    expect(decrypt).not.toHaveBeenCalledWith(expect.objectContaining({ id: malformedId }));
    projection.lock();
  });

  it("quarantines envelope and authentication failures per record without blocking good records", async () => {
    database = await openVaultDatabase();
    await database.putRecord({ ...stored(malformedId, "merchant", null), envelopes: [] });
    await database.putRecord(stored(corruptId, "merchant", null));
    database.close();
    const decrypt = vi.fn(async (record: VaultRecord) => {
      if (record.id === corruptId) throw new Error("bad authentication tag");
      return new Uint8Array(plaintext.get(record.id)!);
    });
    const projection = encryptedVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn() });
    projection.attachCrypto({ ownerId, decrypt, encryptNew: vi.fn(), encryptReplacement: vi.fn() });

    await projection.unlock();

    await expect(projection.listAccounts("personal")).resolves.toContainEqual(expect.objectContaining({ id: accountId }));
    database = await openVaultDatabase();
    await expect(database.getQuarantine(malformedId, 1)).resolves.toMatchObject({ reason: "authentication-failed" });
    await expect(database.getQuarantine(corruptId, 1)).resolves.toMatchObject({ reason: "authentication-failed" });
    database.close();
    projection.lock();
  });

  it("fails the unlock without quarantine when the Worker becomes unavailable", async () => {
    const projection = encryptedVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn() });
    projection.attachCrypto({
      ownerId,
      decrypt: vi.fn().mockRejectedValue(new VaultWorkerUnavailableError()),
      encryptNew: vi.fn(),
      encryptReplacement: vi.fn(),
    });

    await expect(projection.unlock()).rejects.toBeInstanceOf(VaultWorkerUnavailableError);

    expect(projection.state).toBe("error");
    database = await openVaultDatabase();
    await expect(database.quarantineEntries()).resolves.toEqual([]);
    database.close();
  });

  it("queues encrypted create and replacement while keeping the original envelope", async () => {
    const replacement = bytes(50);
    const bridge: ProjectionCryptoBridge = {
      ownerId,
      decrypt: vi.fn(async (record) => new Uint8Array(plaintext.get(record.id)!)),
      encryptNew: vi.fn(async (_record, value) => {
        plaintext.set(createdAccountId, new Uint8Array(value));
        return { sealedPayload: { nonce: bytes(20), ciphertext: bytes(30) }, personalEnvelope: { nonce: bytes(40), ciphertext: replacement } };
      }),
      encryptReplacement: vi.fn(async (_record, value) => {
        plaintext.set(createdAccountId, new Uint8Array(value));
        return { nonce: bytes(60), ciphertext: bytes(70) };
      }),
    };
    const api: SyncMutationApi = {
      create: vi.fn(async ({ records }) => [syncRecord(records[0]!)]),
      replace: vi.fn(async (id, request) => ({ ...syncRecord({ id, idempotencyKey: "ignored", recordType: 0, parentResourceId: id, protocolVersion: 1, nonce: request.nonce, ciphertext: request.ciphertext, personalEnvelope: { wrappedKey: replacement, nonce: bytes(40), protocolVersion: 1 } }), revision: 2 })),
      remove: vi.fn(),
    };
    const projection = encryptedVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn(), syncApi: api, id: () => createdAccountId, now: () => new Date(instant) });
    projection.attachCrypto(bridge);
    await projection.unlock();

    await projection.createAccount("personal", { label: "Savings", currency: Currency.EUR, openingBalanceMinorUnits: 800, isDefault: false });
    const updated = await projection.updateAccount("personal", createdAccountId, { label: "Rainy day", currency: Currency.EUR, openingBalanceMinorUnits: 900, isDefault: false });

    const replacementRecord = vi.mocked(bridge.encryptReplacement).mock.calls[0]![0];
    projection.lock();
    expect(api.create).toHaveBeenCalledOnce();
    expect(api.replace).toHaveBeenCalledOnce();
    expect(updated).toMatchObject({ label: "Rainy day", openingBalanceMinorUnits: 900 });
    expect(replacementRecord.id).toBe(createdAccountId);
    expect(Array.from(replacementRecord.envelopes[0]!.wrappedKey)).toEqual(Array.from(replacement));
  });

  it("rejects changing an existing transaction root account before encryption", async () => {
    const bridge: ProjectionCryptoBridge = {
      ownerId,
      decrypt: vi.fn(async (record) => new Uint8Array(plaintext.get(record.id)!)),
      encryptNew: vi.fn(),
      encryptReplacement: vi.fn(),
    };
    const syncApi: SyncMutationApi = { create: vi.fn(), replace: vi.fn(), remove: vi.fn() };
    const projection = encryptedVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn(), syncApi });
    projection.attachCrypto(bridge);
    await projection.unlock();

    await expect(projection.saveTransaction({
      id: transactionId,
      space: "personal",
      kind: "expense",
      amountMinorUnits: 500,
      currency: Currency.EUR,
      occurredAt: instant,
      accountId: createdAccountId,
      categoryId,
      merchantLabel: "Must not persist",
      tagLabels: ["Must not persist"],
      reason: null,
    })).rejects.toThrow("account cannot be changed");

    expect(bridge.encryptReplacement).not.toHaveBeenCalled();
    expect(bridge.encryptNew).not.toHaveBeenCalled();
    expect(syncApi.create).not.toHaveBeenCalled();
    projection.lock();
  });

  it("rejects stale budget and transaction edits before encryption", async () => {
    const bridge: ProjectionCryptoBridge = { ownerId, decrypt: vi.fn(async (record) => new Uint8Array(plaintext.get(record.id)!)), encryptNew: vi.fn(), encryptReplacement: vi.fn() };
    const projection = encryptedVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn() });
    projection.attachCrypto(bridge);
    await projection.unlock();
    const missing = "cccccccc-cccc-4ccc-8ccc-cccccccccccc";

    await expect(projection.saveBudget("personal", { id: missing, categoryId, amount: { minorUnits: 100, currency: Currency.EUR }, recurrence: "Monthly", startsOn: "2026-08-01", endsOn: null, alertThresholdPercent: null })).rejects.toThrow("budget was not found");
    await expect(projection.saveTransaction({ id: missing, space: "personal", kind: "expense", amountMinorUnits: 100, currency: Currency.EUR, occurredAt: instant, accountId, categoryId, merchantLabel: "Must not persist", tagLabels: ["Must not persist"], reason: null })).rejects.toThrow("transaction was not found");

    expect(bridge.encryptNew).not.toHaveBeenCalled();
    expect(bridge.encryptReplacement).not.toHaveBeenCalled();
    projection.lock();
  });

  it("applies an exact category priority change", async () => {
    const bridge: ProjectionCryptoBridge = {
      ownerId,
      decrypt: vi.fn(async (record) => new Uint8Array(plaintext.get(record.id)!)),
      encryptNew: vi.fn(),
      encryptReplacement: vi.fn(async (_record, value) => {
        plaintext.set(categoryId, new Uint8Array(value));
        return { nonce: bytes(80), ciphertext: bytes(90) };
      }),
    };
    const api: SyncMutationApi = {
      create: vi.fn(),
      replace: vi.fn(async (_id, request) => ({ ...stored(categoryId, "category", null), revision: 2, nonce: request.nonce, ciphertext: request.ciphertext })),
      remove: vi.fn(),
    };
    const projection = encryptedVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn(), syncApi: api, now: () => new Date(instant) });
    projection.attachCrypto(bridge);
    await projection.unlock();

    const changed = await projection.updateTaxonomy("personal", "category", categoryId, { label: "Coffee", priority: "Essential" });

    expect(changed.priority).toBe("Essential");
    projection.lock();
  });

  it("keeps offline ciphertext queued and resumes it after reload", async () => {
    const bridge: ProjectionCryptoBridge = {
      ownerId,
      decrypt: vi.fn(async (record) => new Uint8Array(plaintext.get(record.id)!)),
      encryptNew: vi.fn(async (_record, value) => {
        plaintext.set(createdAccountId, new Uint8Array(value));
        return { sealedPayload: { nonce: bytes(20), ciphertext: bytes(30) }, personalEnvelope: { nonce: bytes(40), ciphertext: bytes(50) } };
      }),
      encryptReplacement: vi.fn(),
    };
    const create = vi.fn()
      .mockRejectedValueOnce(new Error("offline"))
      .mockImplementationOnce(async ({ records }) => [syncRecord(records[0]!)]);
    const api: SyncMutationApi = { create, replace: vi.fn(), remove: vi.fn() };
    const first = encryptedVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn(), syncApi: api, id: () => createdAccountId, now: () => new Date(instant) });
    first.attachCrypto(bridge);
    await first.unlock();

    await expect(first.createAccount("personal", { label: "Offline", currency: Currency.EUR, openingBalanceMinorUnits: 10, isDefault: false })).resolves.toMatchObject({ id: createdAccountId });
    first.lock();

    const second = encryptedVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn(), syncApi: api });
    second.attachCrypto(bridge);
    await second.unlock();
    expect(create).toHaveBeenCalledTimes(2);
    await expect(second.listAccounts("personal")).resolves.toContainEqual(expect.objectContaining({ id: createdAccountId, label: "Offline" }));
    second.lock();
  });

  it("durably stages category and dependent budget tombstones before offline replay", async () => {
    const syncApi: SyncMutationApi = { create: vi.fn(), replace: vi.fn(), remove: vi.fn().mockRejectedValue(new Error("offline")) };
    const projection = encryptedVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn(), syncApi });
    projection.attachCrypto({ ownerId, decrypt: vi.fn(async (record) => new Uint8Array(plaintext.get(record.id)!)), encryptNew: vi.fn(), encryptReplacement: vi.fn() });
    await projection.unlock();

    await projection.deleteTaxonomy("personal", "category", categoryId);

    await expect(projection.listBudgets("personal", new Date(instant))).resolves.toEqual([]);
    projection.lock();
    database = await openVaultDatabase();
    await expect(database.outboxEntries()).resolves.toEqual([
      expect.objectContaining({ mutation: expect.objectContaining({ kind: "delete", record: expect.objectContaining({ id: categoryId }) }) }),
      expect.objectContaining({ mutation: expect.objectContaining({ kind: "delete", record: expect.objectContaining({ id: budgetId }) }) }),
    ]);
    database.close();
  });

  it("aborts pull and clears decrypted state on lock", async () => {
    let observedSignal: AbortSignal | undefined;
    const projection = encryptedVaultProjection(new SyncLifecycleCoordinator(), {
      pull: async (_database, _bridge, signal) => {
        observedSignal = signal;
        await new Promise<void>((resolve) => signal.addEventListener("abort", () => resolve(), { once: true }));
        throw new DOMException("cancelled", "AbortError");
      },
    });
    projection.attachCrypto({ ownerId, decrypt: vi.fn(), encryptNew: vi.fn(), encryptReplacement: vi.fn() });
    const unlocking = projection.unlock().catch(() => undefined);

    await vi.waitFor(() => expect(observedSignal).toBeDefined());
    projection.lock();
    await unlocking;

    expect(observedSignal?.aborted).toBe(true);
    expect(projection.state).toBe("locked");
    expect(() => projection.listAccounts("personal")).toThrow("locked");
  });
});
