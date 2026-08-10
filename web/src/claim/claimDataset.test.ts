import { describe, expect, it } from "vitest";
import { hashClaimManifest } from "./manifest";
import {
  assertPreparedClaimPayloadSizes,
  maximumPreparedClaimPayloadBytes,
  parseClaimDataset,
  prepareClaimRecords,
  recomputeCurrentBalances,
  type PreparedClaimRecord,
} from "./claimDataset";

const ids = {
  firstAccount: "11111111-1111-4111-8111-111111111111",
  secondAccount: "22222222-2222-4222-8222-222222222222",
  expense: "33333333-3333-4333-8333-333333333333",
  income: "44444444-4444-4444-8444-444444444444",
  transfer: "55555555-5555-4555-8555-555555555555",
};

const timestamp = "2026-08-09T10:00:00Z";

const dataset = async (): Promise<Record<string, unknown>> => {
  const identities = [
    { recordType: "account", id: ids.firstAccount },
    { recordType: "account", id: ids.secondAccount },
    { recordType: "transaction", id: ids.expense },
    { recordType: "transaction", id: ids.income },
    { recordType: "transfer", id: ids.transfer },
  ];
  return {
    protocolVersion: 1,
    recordCount: 5,
    counts: {
      account: 2,
      necessityScale: 0,
      category: 0,
      merchant: 0,
      tag: 0,
      transaction: 2,
      transfer: 1,
      budget: 0,
      notification: 0,
    },
    manifestHash: await hashClaimManifest(identities),
    accounts: [
      {
        id: ids.firstAccount,
        legacyId: 1,
        isDeleted: false,
        createdAt: timestamp,
        updatedAt: null,
        label: "Cash",
        accountNumber: "A-1",
        balanceMinorUnits: 700,
        currency: 0,
        isDefault: true,
      },
      {
        id: ids.secondAccount,
        legacyId: 2,
        isDeleted: false,
        createdAt: timestamp,
        updatedAt: null,
        label: "Savings",
        accountNumber: "A-2",
        balanceMinorUnits: 750,
        currency: 0,
        isDefault: false,
      },
    ],
    necessityScales: [],
    categories: [],
    merchants: [],
    tags: [],
    transactions: [
      {
        id: ids.expense,
        legacyId: 3,
        isDeleted: false,
        createdAt: timestamp,
        updatedAt: null,
        kind: 1,
        amountMinorUnits: 100,
        currency: 0,
        occurredAt: timestamp,
        reason: null,
        sourceAccountLegacyId: 1,
        sourceAccountId: ids.firstAccount,
        destinationAccountLegacyId: null,
        destinationAccountId: null,
        categoryLegacyId: null,
        categoryId: null,
        merchantLegacyId: null,
        merchantId: null,
        tagLegacyIds: [],
        tagIds: [],
      },
      {
        id: ids.income,
        legacyId: 4,
        isDeleted: false,
        createdAt: timestamp,
        updatedAt: null,
        kind: 0,
        amountMinorUnits: 50,
        currency: 0,
        occurredAt: timestamp,
        reason: null,
        sourceAccountLegacyId: null,
        sourceAccountId: null,
        destinationAccountLegacyId: 2,
        destinationAccountId: ids.secondAccount,
        categoryLegacyId: null,
        categoryId: null,
        merchantLegacyId: null,
        merchantId: null,
        tagLegacyIds: [],
        tagIds: [],
      },
      {
        id: ids.transfer,
        legacyId: 5,
        isDeleted: false,
        createdAt: timestamp,
        updatedAt: null,
        kind: 2,
        amountMinorUnits: 200,
        currency: 0,
        occurredAt: timestamp,
        reason: "Move",
        sourceAccountLegacyId: 1,
        sourceAccountId: ids.firstAccount,
        destinationAccountLegacyId: 2,
        destinationAccountId: ids.secondAccount,
        categoryLegacyId: null,
        categoryId: null,
        merchantLegacyId: null,
        merchantId: null,
        tagLegacyIds: [],
        tagIds: [],
      },
    ],
    budgets: [],
    notifications: [],
  };
};

describe("legacy claim dataset", () => {
  it("accepts the largest prepared payload that fits the sync ciphertext limit", () => {
    const envelopeBytes = new TextEncoder().encode(JSON.stringify({ value: "" })).length;
    const record: PreparedClaimRecord = {
      id: ids.expense,
      recordType: "notification",
      recordTypeCode: 7,
      parentResourceId: null,
      tombstone: false,
      createdAt: timestamp,
      updatedAt: null,
      payload: { value: "x".repeat(maximumPreparedClaimPayloadBytes - envelopeBytes) },
    };

    expect(() => assertPreparedClaimPayloadSizes([record])).not.toThrow();
  });

  it("rejects a prepared payload one byte over the sync ciphertext limit", () => {
    const envelopeBytes = new TextEncoder().encode(JSON.stringify({ value: "" })).length;
    const record: PreparedClaimRecord = {
      id: ids.expense,
      recordType: "notification",
      recordTypeCode: 7,
      parentResourceId: null,
      tombstone: false,
      createdAt: timestamp,
      updatedAt: null,
      payload: { value: "x".repeat(maximumPreparedClaimPayloadBytes + 1 - envelopeBytes) },
    };

    expect(() => assertPreparedClaimPayloadSizes([record])).toThrow(
      `Legacy notification ${ids.expense}: The prepared encrypted payload must not exceed 65520 UTF-8 bytes.`,
    );
  });

  it("validates the complete dataset before producing versioned projection payloads", async () => {
    const parsed = await parseClaimDataset(await dataset());
    const prepared = prepareClaimRecords(parsed);

    expect(prepared).toHaveLength(5);
    expect(prepared.map((record) => record.id)).toEqual([
      ids.firstAccount,
      ids.secondAccount,
      ids.expense,
      ids.income,
      ids.transfer,
    ]);
    expect(prepared[0]).toMatchObject({
      recordType: "account",
      recordTypeCode: 0,
      parentResourceId: ids.firstAccount,
      tombstone: false,
      payload: {
        schemaVersion: 1,
        recordId: ids.firstAccount,
        label: "Cash",
        openingBalanceMinorUnits: 1000,
        balance: { minorUnits: 700, currency: "EUR" },
      },
    });
    expect(prepared[2]).toMatchObject({
      recordType: "transaction",
      recordTypeCode: 1,
      parentResourceId: ids.firstAccount,
      payload: { schemaVersion: 1, id: 3, kind: "expense" },
    });
  });

  it("derives opening balances by reversing the active ledger and recomputes exact current balances", async () => {
    const parsed = await parseClaimDataset(await dataset());
    const prepared = prepareClaimRecords(parsed);
    const openings = new Map(
      prepared
        .filter((record) => record.recordType === "account")
        .map((record) => [record.id, record.payload.openingBalanceMinorUnits as number]),
    );

    expect(openings.get(ids.firstAccount)).toBe(1000);
    expect(openings.get(ids.secondAccount)).toBe(500);
    expect(recomputeCurrentBalances(parsed, openings)).toEqual(
      new Map([
        [ids.firstAccount, 700],
        [ids.secondAccount, 750],
      ]),
    );
  });

  it("reports the exact legacy record before any encryption when a domain rule is broken", async () => {
    const invalid = await dataset();
    const transactions = invalid.transactions as Array<Record<string, unknown>>;
    transactions[0]!.currency = 1;

    await expect(parseClaimDataset(invalid)).rejects.toThrow(
      `Legacy transaction ${ids.expense}: The transaction currency must match the account currency.`,
    );
  });

  it("rejects a dataset whose declared manifest does not match its UUID identities", async () => {
    const invalid = await dataset();
    invalid.manifestHash = "0".repeat(64);

    await expect(parseClaimDataset(invalid)).rejects.toThrow(
      "The legacy dataset manifest does not match its records.",
    );
  });
});
