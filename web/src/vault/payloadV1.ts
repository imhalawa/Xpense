import type { RecordType } from "../crypto/protocol";
import { Currency } from "../typings/enums/Currency";

const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;
const encoder = new TextEncoder();
const decoder = new TextDecoder();

export interface VaultPayloadV1 {
  schemaVersion: 1;
  recordId: string;
  createdAt: string;
  updatedAt: string | null;
  [key: string]: unknown;
}

const invalid = (recordType: RecordType, recordId: string): never => {
  throw new Error(`Encrypted ${recordType} ${recordId}: The payload is invalid.`);
};

const objectValue = (
  value: unknown,
  recordType: RecordType,
  recordId: string,
): Record<string, unknown> => {
  if (value === null || typeof value !== "object" || Array.isArray(value)) {
    invalid(recordType, recordId);
  }
  return value as Record<string, unknown>;
};

const stringValue = (value: unknown, recordType: RecordType, recordId: string): string =>
  typeof value === "string" ? value : invalid(recordType, recordId);

const nullableString = (
  value: unknown,
  recordType: RecordType,
  recordId: string,
): string | null => value === null ? null : stringValue(value, recordType, recordId);

const numberValue = (value: unknown, recordType: RecordType, recordId: string): number =>
  typeof value === "number" && Number.isSafeInteger(value)
    ? value
    : invalid(recordType, recordId);

const nullableNumber = (
  value: unknown,
  recordType: RecordType,
  recordId: string,
): number | null => value === null ? null : numberValue(value, recordType, recordId);

const booleanValue = (value: unknown, recordType: RecordType, recordId: string): boolean =>
  typeof value === "boolean" ? value : invalid(recordType, recordId);

const uuidValue = (value: unknown, recordType: RecordType, recordId: string): string => {
  const candidate = stringValue(value, recordType, recordId).toLowerCase();
  return uuidPattern.test(candidate) ? candidate : invalid(recordType, recordId);
};

const nullableUuid = (
  value: unknown,
  recordType: RecordType,
  recordId: string,
): string | null => value === null ? null : uuidValue(value, recordType, recordId);

const currencyValue = (value: unknown, recordType: RecordType, recordId: string): Currency =>
  value === Currency.EUR || value === Currency.USD ? value : invalid(recordType, recordId);

const moneyValue = (
  value: unknown,
  recordType: RecordType,
  recordId: string,
): { minorUnits: number; currency: Currency } => {
  const money = objectValue(value, recordType, recordId);
  return {
    minorUnits: numberValue(money.minorUnits, recordType, recordId),
    currency: currencyValue(money.currency, recordType, recordId),
  };
};

const uuidArray = (value: unknown, recordType: RecordType, recordId: string): string[] => {
  if (!Array.isArray(value)) invalid(recordType, recordId);
  const entries = value as unknown[];
  return entries.map((entry: unknown) => uuidValue(entry, recordType, recordId));
};

const common = (
  recordType: RecordType,
  recordId: string,
  value: unknown,
): VaultPayloadV1 => {
  const payload = objectValue(value, recordType, recordId);
  if (payload.schemaVersion !== 1 || uuidValue(payload.recordId, recordType, recordId) !== recordId) {
    invalid(recordType, recordId);
  }
  return {
    ...payload,
    schemaVersion: 1,
    recordId,
    createdAt: stringValue(payload.createdAt, recordType, recordId),
    updatedAt: nullableString(payload.updatedAt, recordType, recordId),
  } as VaultPayloadV1;
};

export const parseVaultPayloadV1 = (
  recordType: RecordType,
  recordId: string,
  value: unknown,
): VaultPayloadV1 => {
  const payload = common(recordType, recordId, value);
  switch (recordType) {
    case "account": {
      const balance = moneyValue(payload.balance, recordType, recordId);
      const openingBalanceMinorUnits = numberValue(
        payload.openingBalanceMinorUnits,
        recordType,
        recordId,
      );
      if (currencyValue(payload.currency, recordType, recordId) !== balance.currency) {
        invalid(recordType, recordId);
      }
      return {
        ...payload,
        label: stringValue(payload.label, recordType, recordId),
        balance,
        openingBalanceMinorUnits,
        currency: balance.currency,
        isDefault: booleanValue(payload.isDefault, recordType, recordId),
      };
    }
    case "category": {
      const priority = objectValue(payload.priority, recordType, recordId);
      return {
        ...payload,
        label: stringValue(payload.label, recordType, recordId),
        necessityScaleId: uuidValue(payload.necessityScaleId, recordType, recordId),
        priority: {
          ...priority,
          label: stringValue(priority.label, recordType, recordId),
        },
      };
    }
    case "merchant":
      return { ...payload, label: stringValue(payload.label, recordType, recordId) };
    case "tag":
      return {
        ...payload,
        label: stringValue(payload.label, recordType, recordId),
        backgroundColorHex: nullableString(payload.backgroundColorHex, recordType, recordId),
        foregroundColorHex: nullableString(payload.foregroundColorHex, recordType, recordId),
      };
    case "transaction":
    case "transfer": {
      const kind = stringValue(payload.kind, recordType, recordId);
      if (kind !== "income" && kind !== "expense" && kind !== "transfer") {
        invalid(recordType, recordId);
      }
      if ((recordType === "transfer") !== (kind === "transfer")) {
        invalid(recordType, recordId);
      }
      const sourceAccountId = nullableUuid(payload.sourceAccountId, recordType, recordId);
      const destinationAccountId = nullableUuid(payload.destinationAccountId, recordType, recordId);
      if (
        (kind === "income" && (sourceAccountId !== null || destinationAccountId === null)) ||
        (kind === "expense" && (sourceAccountId === null || destinationAccountId !== null)) ||
        (kind === "transfer" && (
          sourceAccountId === null ||
          destinationAccountId === null ||
          sourceAccountId === destinationAccountId
        ))
      ) {
        invalid(recordType, recordId);
      }
      return {
        ...payload,
        kind,
        amount: moneyValue(payload.amount, recordType, recordId),
        sourceAccountId,
        destinationAccountId,
        categoryRecordId: nullableUuid(payload.categoryRecordId, recordType, recordId),
        merchantRecordId: nullableUuid(payload.merchantRecordId, recordType, recordId),
        tagRecordIds: uuidArray(payload.tagRecordIds, recordType, recordId),
        reason: nullableString(payload.reason, recordType, recordId),
        occurredAt: stringValue(payload.occurredAt, recordType, recordId),
      };
    }
    case "budget": {
      const recurrence = stringValue(payload.recurrence, recordType, recordId);
      if (!["None", "Weekly", "Monthly", "Yearly"].includes(recurrence)) {
        invalid(recordType, recordId);
      }
      return {
        ...payload,
        categoryRecordId: uuidValue(payload.categoryRecordId, recordType, recordId),
        amount: moneyValue(payload.amount, recordType, recordId),
        recurrence,
        startsOn: stringValue(payload.startsOn, recordType, recordId),
        endsOn: nullableString(payload.endsOn, recordType, recordId),
        alertThresholdPercent: nullableNumber(
          payload.alertThresholdPercent,
          recordType,
          recordId,
        ),
      };
    }
    case "notification":
      return {
        ...payload,
        kind: stringValue(payload.kind, recordType, recordId),
        title: stringValue(payload.title, recordType, recordId),
        message: stringValue(payload.message, recordType, recordId),
        readAt: nullableString(payload.readAt, recordType, recordId),
      };
    case "necessityScale":
      return {
        ...payload,
        label: stringValue(payload.label, recordType, recordId),
        weight: typeof payload.weight === "number" && Number.isFinite(payload.weight)
          ? payload.weight
          : invalid(recordType, recordId),
      };
    case "userProfile":
      return payload;
  }
};

export const decodeVaultPayloadV1 = (
  recordType: RecordType,
  recordId: string,
  bytes: Uint8Array,
): VaultPayloadV1 => {
  try {
    return parseVaultPayloadV1(recordType, recordId, JSON.parse(decoder.decode(bytes)));
  } catch (error) {
    if (error instanceof Error && error.message.startsWith("Encrypted ")) throw error;
    return invalid(recordType, recordId);
  }
};

export const encodeVaultPayloadV1 = (payload: VaultPayloadV1): Uint8Array<ArrayBuffer> =>
  encoder.encode(JSON.stringify(payload));
