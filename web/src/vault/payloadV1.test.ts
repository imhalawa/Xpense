import { describe, expect, it } from "vitest";
import type { RecordType } from "../crypto/protocol";
import { decodeVaultPayloadV1, encodeVaultPayloadV1, parseVaultPayloadV1 } from "./payloadV1";

const ids = {
  record: "11111111-1111-4111-8111-111111111111",
  account: "22222222-2222-4222-8222-222222222222",
  other: "33333333-3333-4333-8333-333333333333",
};
const common = { schemaVersion: 1, recordId: ids.record, createdAt: "2026-08-09T10:00:00Z", updatedAt: null };
const cases: Array<[RecordType, Record<string, unknown>]> = [
  ["account", { ...common, label: "Wallet", balance: { minorUnits: 25, currency: "EUR" }, openingBalanceMinorUnits: 10, currency: "EUR", isDefault: true }],
  ["necessityScale", { ...common, label: "Useful", weight: 2.5 }],
  ["category", { ...common, label: "Food", necessityScaleId: ids.other, priority: { label: "Useful" } }],
  ["merchant", { ...common, label: "Albert Heijn" }],
  ["tag", { ...common, label: "Shopping", foregroundColorHex: "#111111", backgroundColorHex: null }],
  ["transaction", { ...common, kind: "expense", amount: { minorUnits: 500, currency: "EUR" }, sourceAccountId: ids.account, destinationAccountId: null, categoryRecordId: ids.other, merchantRecordId: null, tagRecordIds: [ids.other], reason: null, occurredAt: "2026-08-09T10:00:00Z" }],
  ["transfer", { ...common, kind: "transfer", amount: { minorUnits: 500, currency: "EUR" }, sourceAccountId: ids.account, destinationAccountId: ids.other, categoryRecordId: null, merchantRecordId: null, tagRecordIds: [], reason: "Move", occurredAt: "2026-08-09T10:00:00Z" }],
  ["budget", { ...common, categoryRecordId: ids.other, amount: { minorUnits: 1000, currency: "EUR" }, recurrence: "Monthly", startsOn: "2026-08-01", endsOn: null, alertThresholdPercent: 80 }],
  ["notification", { ...common, kind: "budget", title: "Limit", message: "Near limit", readAt: null }],
  ["userProfile", { ...common, theme: "dark" }],
];

describe("vault payload v1", () => {
  it.each(cases)("round-trips UUID-native %s payloads", (recordType, payload) => {
    const parsed = parseVaultPayloadV1(recordType, ids.record, payload);
    expect(decodeVaultPayloadV1(recordType, ids.record, encodeVaultPayloadV1(parsed))).toEqual(parsed);
  });

  it("rejects legacy numeric relationship identifiers", () => {
    const payload = { ...cases.find(([type]) => type === "transaction")![1], categoryRecordId: 42 };
    expect(() => parseVaultPayloadV1("transaction", ids.record, payload)).toThrow("payload is invalid");
  });

  it.each([
    ["transaction" as const, { ...cases.find(([type]) => type === "transfer")![1] }],
    ["transfer" as const, { ...cases.find(([type]) => type === "transaction")![1] }],
    ["transfer" as const, { ...cases.find(([type]) => type === "transfer")![1], destinationAccountId: ids.account }],
  ])("rejects mismatched or self-referencing %s payloads", (recordType, payload) => {
    expect(() => parseVaultPayloadV1(recordType, ids.record, payload)).toThrow("payload is invalid");
  });
});
