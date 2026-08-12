import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { SyncLifecycleCoordinator } from "../sync/lifecycle";
import type { SyncMutationApi } from "../sync/outbox";
import type { SyncRecord } from "../sync/syncClient";
import { Currency } from "../typings/enums/Currency";
import { encodeVaultPayloadV1, type VaultPayloadV1 } from "./payloadV1";
import { deleteVaultDatabase, openVaultDatabase, type VaultDatabase, type VaultRecord } from "./vaultDatabase";
import { localVaultProjection } from "./localVaultProjection";

const ownerId = "11111111-1111-4111-8111-111111111111";
const accountId = "22222222-2222-4222-8222-222222222222";
const scaleId = "33333333-3333-4333-8333-333333333333";
const categoryId = "44444444-4444-4444-8444-444444444444";
const transactionId = "55555555-5555-4555-8555-555555555555";
const createdAccountId = "66666666-6666-4666-8666-666666666666";
const essentialScaleId = "88888888-8888-4888-8888-888888888888";
const malformedId = "99999999-9999-4999-8999-999999999999";
const budgetId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const instant = "2026-08-09T10:00:00.000Z";
const payload = (recordId: string, fields: Record<string, unknown>): VaultPayloadV1 => ({ schemaVersion: 1, recordId, createdAt: instant, updatedAt: null, ...fields });

const seeded: Record<string, VaultPayloadV1> = {
  [accountId]: payload(accountId, { label: "Wallet", balance: { minorUnits: 500, currency: "EUR" }, openingBalanceMinorUnits: 1000, currency: "EUR", isDefault: true }),
  [scaleId]: payload(scaleId, { label: "Useful", weight: 2 }),
  [essentialScaleId]: payload(essentialScaleId, { label: "Essential", weight: 5 }),
  [categoryId]: payload(categoryId, { label: "Coffee", necessityScaleId: scaleId, priority: { label: "Useful" } }),
  [transactionId]: payload(transactionId, { kind: "expense", amount: { minorUnits: 500, currency: "EUR" }, sourceAccountId: accountId, destinationAccountId: null, categoryRecordId: categoryId, merchantRecordId: null, tagRecordIds: [], reason: null, occurredAt: instant }),
  [budgetId]: payload(budgetId, { categoryRecordId: categoryId, amount: { minorUnits: 900, currency: "EUR" }, recurrence: "Monthly", startsOn: "2026-08-01", endsOn: null, alertThresholdPercent: null }),
};

const stored = (
  id: string,
  recordType: VaultRecord["recordType"],
  parentResourceId: string | null,
  bytes: Uint8Array = encodeVaultPayloadV1(seeded[id]!),
): VaultRecord => ({ id, recordType, ownerId, parentResourceId, revision: 1, payload: bytes, tombstone: false, sequenceNumber: 1, serverCreatedAt: instant, serverUpdatedAt: instant });

let database: VaultDatabase;

beforeEach(async () => {
  await deleteVaultDatabase();
  database = await openVaultDatabase();
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
  payload: request.payload,
  tombstone: false,
  sequenceNumber: 2,
  serverCreatedAt: instant,
  serverUpdatedAt: instant,
});

describe("local vault projection", () => {
  it("rebuilds UUID-native balances from IndexedDB without a legacy client", async () => {
    const projection = localVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn() });

    await projection.unlock();

    await expect(projection.listAccounts("personal")).resolves.toMatchObject([{ id: accountId, openingBalanceMinorUnits: 1000 }]);
    await expect(projection.listAccountBalances("personal")).resolves.toEqual({ state: "available", balances: [{ currency: Currency.EUR, minorUnits: 500 }] });
    await expect(projection.listTransactions("personal")).resolves.toMatchObject([{ id: transactionId, accountId, categoryId }]);
    projection.lock();
  });

  it("quarantines a malformed payload and still renders good records", async () => {
    database = await openVaultDatabase();
    await database.putRecord(stored(malformedId, "merchant", null, new TextEncoder().encode('{"schemaVersion":1}')));
    database.close();
    const projection = localVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn() });

    await projection.unlock();

    await expect(projection.listAccounts("personal")).resolves.toContainEqual(expect.objectContaining({ id: accountId }));
    database = await openVaultDatabase();
    await expect(database.getQuarantine(malformedId, 1)).resolves.toMatchObject({ reason: "invalid-payload" });
    database.close();
    projection.lock();
  });

  it("skips a record already covered by a quarantine entry", async () => {
    database = await openVaultDatabase();
    const quarantinedRecord = stored(malformedId, "merchant", null, new TextEncoder().encode('{"schemaVersion":1}'));
    await database.putRecord(quarantinedRecord);
    await database.putQuarantine({ recordId: malformedId, revision: 1, reason: "invalid-payload" });
    database.close();
    const projection = localVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn() });

    await projection.unlock();

    await expect(projection.listTaxonomy("personal", "merchant")).resolves.toEqual([]);
    projection.lock();
  });

  it("moves to error and quarantines nothing when the pull fails", async () => {
    const projection = localVaultProjection(new SyncLifecycleCoordinator(), {
      pull: vi.fn().mockRejectedValue(new Error("offline")),
    });

    await expect(projection.unlock()).rejects.toThrow("offline");

    expect(projection.state).toBe("error");
    database = await openVaultDatabase();
    await expect(database.quarantineEntries()).resolves.toEqual([]);
    database.close();
  });

  it("queues a create and a replacement as plaintext payloads", async () => {
    const api: SyncMutationApi = {
      create: vi.fn(async ({ records }) => [syncRecord(records[0]!)]),
      replace: vi.fn(async (id, request) => ({ ...syncRecord({ id, idempotencyKey: "ignored", recordType: 0, parentResourceId: id, payload: request.payload }), revision: 2 })),
      remove: vi.fn(),
    };
    const projection = localVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn(), syncApi: api, id: () => createdAccountId, now: () => new Date(instant) });
    await projection.unlock();

    await projection.createAccount("personal", { label: "Savings", currency: Currency.EUR, openingBalanceMinorUnits: 800, isDefault: false });
    const updated = await projection.updateAccount("personal", createdAccountId, { label: "Rainy day", currency: Currency.EUR, openingBalanceMinorUnits: 900, isDefault: false });

    expect(api.create).toHaveBeenCalledOnce();
    expect(api.replace).toHaveBeenCalledOnce();
    expect(updated).toMatchObject({ label: "Rainy day", openingBalanceMinorUnits: 900 });
    const createdPayload = vi.mocked(api.create).mock.calls[0]![0].records[0]!.payload;
    expect(new TextDecoder().decode(createdPayload)).toContain("Savings");
    const replacedPayload = vi.mocked(api.replace).mock.calls[0]![1].payload;
    expect(new TextDecoder().decode(replacedPayload)).toContain("Rainy day");
    projection.lock();
  });

  it("leaves exactly one default account when a new one claims the flag", async () => {
    const api: SyncMutationApi = {
      create: vi.fn(async ({ records }) => [syncRecord(records[0]!)]),
      replace: vi.fn(async (id, request) => ({ ...syncRecord({ id, idempotencyKey: "ignored", recordType: 0, parentResourceId: id, payload: request.payload }), revision: 2 })),
      remove: vi.fn(),
    };
    const projection = localVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn(), syncApi: api, id: () => createdAccountId, now: () => new Date(instant) });
    await projection.unlock();

    const before = await projection.listAccounts("personal");
    await projection.createAccount("personal", { label: "Savings", currency: Currency.EUR, openingBalanceMinorUnits: 800, isDefault: true });
    const after = await projection.listAccounts("personal");
    projection.lock();

    expect(before.filter((account) => account.isDefault).map((account) => account.label)).toEqual(["Wallet"]);
    expect(after.filter((account) => account.isDefault).map((account) => account.label)).toEqual(["Savings"]);
  });

  it("rejects changing an existing transaction root account before queueing", async () => {
    const syncApi: SyncMutationApi = { create: vi.fn(), replace: vi.fn(), remove: vi.fn() };
    const projection = localVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn(), syncApi });
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

    expect(syncApi.create).not.toHaveBeenCalled();
    projection.lock();
  });

  it("rejects stale budget and transaction edits before queueing", async () => {
    const syncApi: SyncMutationApi = { create: vi.fn(), replace: vi.fn(), remove: vi.fn() };
    const projection = localVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn(), syncApi });
    await projection.unlock();
    const missing = "cccccccc-cccc-4ccc-8ccc-cccccccccccc";

    await expect(projection.saveBudget("personal", { id: missing, categoryId, amount: { minorUnits: 100, currency: Currency.EUR }, recurrence: "Monthly", startsOn: "2026-08-01", endsOn: null, alertThresholdPercent: null })).rejects.toThrow("budget was not found");
    await expect(projection.saveTransaction({ id: missing, space: "personal", kind: "expense", amountMinorUnits: 100, currency: Currency.EUR, occurredAt: instant, accountId, categoryId, merchantLabel: "Must not persist", tagLabels: ["Must not persist"], reason: null })).rejects.toThrow("transaction was not found");

    expect(syncApi.create).not.toHaveBeenCalled();
    expect(syncApi.replace).not.toHaveBeenCalled();
    projection.lock();
  });

  it("applies an exact category priority change", async () => {
    const api: SyncMutationApi = {
      create: vi.fn(),
      replace: vi.fn(async (_id, request) => ({ ...stored(categoryId, "category", null, request.payload), revision: 2 })),
      remove: vi.fn(),
    };
    const projection = localVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn(), syncApi: api, now: () => new Date(instant) });
    await projection.unlock();

    const changed = await projection.updateTaxonomy("personal", "category", categoryId, { label: "Coffee", priority: "Essential" });

    expect(changed.priority).toBe("Essential");
    projection.lock();
  });

  it("keeps an offline create queued and resumes it after reload", async () => {
    const create = vi.fn()
      .mockRejectedValueOnce(new Error("offline"))
      .mockImplementationOnce(async ({ records }) => [syncRecord(records[0]!)]);
    const api: SyncMutationApi = { create, replace: vi.fn(), remove: vi.fn() };
    const first = localVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn(), syncApi: api, id: () => createdAccountId, now: () => new Date(instant) });
    await first.unlock();

    await expect(first.createAccount("personal", { label: "Offline", currency: Currency.EUR, openingBalanceMinorUnits: 10, isDefault: false })).resolves.toMatchObject({ id: createdAccountId });
    first.lock();

    const second = localVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn(), syncApi: api });
    await second.unlock();
    expect(create).toHaveBeenCalledTimes(2);
    await expect(second.listAccounts("personal")).resolves.toContainEqual(expect.objectContaining({ id: createdAccountId, label: "Offline" }));
    second.lock();
  });

  it("durably stages category and dependent budget tombstones before offline replay", async () => {
    const syncApi: SyncMutationApi = { create: vi.fn(), replace: vi.fn(), remove: vi.fn().mockRejectedValue(new Error("offline")) };
    const projection = localVaultProjection(new SyncLifecycleCoordinator(), { pull: vi.fn(), syncApi });
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

  it("aborts pull and clears loaded state on lock", async () => {
    let observedSignal: AbortSignal | undefined;
    const projection = localVaultProjection(new SyncLifecycleCoordinator(), {
      pull: async (_database, signal) => {
        observedSignal = signal;
        await new Promise<void>((resolve) => signal.addEventListener("abort", () => resolve(), { once: true }));
        throw new DOMException("cancelled", "AbortError");
      },
    });
    const unlocking = projection.unlock().catch(() => undefined);

    await vi.waitFor(() => expect(observedSignal).toBeDefined());
    projection.lock();
    await unlocking;

    expect(observedSignal?.aborted).toBe(true);
    expect(projection.state).toBe("locked");
    expect(() => projection.listAccounts("personal")).toThrow("locked");
  });
});
