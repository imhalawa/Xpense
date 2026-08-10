import { validateBudgetWindow } from "../domain/budgetSpending";
import { Currency } from "../typings/enums/Currency";
import type { RecordType } from "../crypto/protocol";
import { parseVaultPayloadV1 } from "../vault/payloadV1";
import { hashClaimManifest, type ClaimRecordIdentity } from "./manifest";

const protocolVersion = 1;
const maximumSyncCiphertextBytes = 65_536;
const aesGcmAuthenticationTagBytes = 16;
export const maximumPreparedClaimPayloadBytes =
  maximumSyncCiphertextBytes - aesGcmAuthenticationTagBytes;
const encoder = new TextEncoder();
const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;
const manifestHashPattern = /^[0-9a-f]{64}$/;
const recordTypeCodes: Record<RecordType, number> = {
  account: 0,
  transaction: 1,
  transfer: 2,
  category: 3,
  merchant: 4,
  tag: 5,
  budget: 6,
  notification: 7,
  userProfile: 8,
  necessityScale: 9,
};
const requiredCountKeys = [
  "account",
  "necessityScale",
  "category",
  "merchant",
  "tag",
  "transaction",
  "transfer",
  "budget",
  "notification",
] as const;

type ClaimRecordType = Exclude<RecordType, "userProfile">;
type Nullable<T> = T | null;

interface LegacyBaseRecord {
  id: string;
  legacyId: number;
  isDeleted: boolean;
  createdAt: string;
  updatedAt: Nullable<string>;
}

export interface LegacyAccountRecord extends LegacyBaseRecord {
  label: string;
  accountNumber: string;
  balanceMinorUnits: number;
  currency: 0 | 1;
  isDefault: boolean;
}

export interface LegacyNecessityScaleRecord extends LegacyBaseRecord {
  label: string;
  weight: number;
}

export interface LegacyCategoryRecord extends LegacyBaseRecord {
  label: string;
  necessityScaleLegacyId: number;
  necessityScaleId: string;
}

export interface LegacyMerchantRecord extends LegacyBaseRecord {
  label: string;
}

export interface LegacyTagRecord extends LegacyBaseRecord {
  label: string;
  backgroundColorHex: Nullable<string>;
  foregroundColorHex: Nullable<string>;
}

export interface LegacyTransactionRecord extends LegacyBaseRecord {
  kind: 0 | 1 | 2;
  amountMinorUnits: number;
  currency: 0 | 1;
  occurredAt: string;
  reason: Nullable<string>;
  sourceAccountLegacyId: Nullable<number>;
  sourceAccountId: Nullable<string>;
  destinationAccountLegacyId: Nullable<number>;
  destinationAccountId: Nullable<string>;
  categoryLegacyId: Nullable<number>;
  categoryId: Nullable<string>;
  merchantLegacyId: Nullable<number>;
  merchantId: Nullable<string>;
  tagLegacyIds: number[];
  tagIds: string[];
}

export interface LegacyBudgetRecord extends LegacyBaseRecord {
  amountMinorUnits: number;
  currency: 0 | 1;
  categoryLegacyId: number;
  categoryId: string;
  recurrence: 0 | 1 | 2 | 3;
  startsOn: string;
  endsOn: Nullable<string>;
  alertThresholdPercent: Nullable<number>;
}

export interface LegacyNotificationRecord extends LegacyBaseRecord {
  ownerLegacyId: Nullable<number>;
  eventId: string;
  kind: number;
  title: string;
  message: string;
  notificationPayload: string;
  payloadHash: string;
  readAt: Nullable<string>;
}

export interface LegacyClaimDataset {
  protocolVersion: 1;
  recordCount: number;
  counts: Record<(typeof requiredCountKeys)[number], number>;
  manifestHash: string;
  accounts: LegacyAccountRecord[];
  necessityScales: LegacyNecessityScaleRecord[];
  categories: LegacyCategoryRecord[];
  merchants: LegacyMerchantRecord[];
  tags: LegacyTagRecord[];
  transactions: LegacyTransactionRecord[];
  budgets: LegacyBudgetRecord[];
  notifications: LegacyNotificationRecord[];
}

export interface PreparedClaimRecord {
  id: string;
  recordType: ClaimRecordType;
  recordTypeCode: number;
  parentResourceId: string | null;
  tombstone: boolean;
  createdAt: string;
  updatedAt: string | null;
  payload: Record<string, unknown>;
}

export const assertPreparedClaimPayloadSizes = (records: PreparedClaimRecord[]): void => {
  for (const record of records) {
    const payloadBytes = encoder.encode(JSON.stringify(record.payload)).byteLength;
    if (payloadBytes > maximumPreparedClaimPayloadBytes) {
      invalid(
        `Legacy ${record.recordType} ${record.id}: The prepared encrypted payload must not exceed ${maximumPreparedClaimPayloadBytes} UTF-8 bytes.`,
      );
    }
  }
};

const invalid = (message: string): never => {
  throw new Error(message);
};

const objectValue = (value: unknown, name: string): Record<string, unknown> => {
  if (value === null || typeof value !== "object" || Array.isArray(value)) {
    invalid(`${name} must be an object.`);
  }
  return value as Record<string, unknown>;
};

const arrayValue = (value: unknown, name: string): unknown[] =>
  Array.isArray(value) ? value : invalid(`${name} must be an array.`);

const stringValue = (value: unknown, name: string, allowEmpty = false): string => {
  if (typeof value !== "string" || (!allowEmpty && value.length === 0)) {
    return invalid(`${name} must be a non-empty string.`);
  }
  return value;
};

const nullableString = (value: unknown, name: string): string | null =>
  value === null ? null : stringValue(value, name, true);

const integerValue = (value: unknown, name: string): number => {
  if (typeof value !== "number" || !Number.isSafeInteger(value)) {
    return invalid(`${name} must be a safe integer.`);
  }
  return value;
};

const nullableInteger = (value: unknown, name: string): number | null =>
  value === null ? null : integerValue(value, name);

const finiteNumber = (value: unknown, name: string): number => {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    return invalid(`${name} must be a finite number.`);
  }
  return value;
};

const booleanValue = (value: unknown, name: string): boolean =>
  typeof value === "boolean" ? value : invalid(`${name} must be true or false.`);

const uuidValue = (value: unknown, name: string): string => {
  const id = stringValue(value, name).toLowerCase();
  return uuidPattern.test(id) ? id : invalid(`${name} must be a valid UUID.`);
};

const nullableUuid = (value: unknown, name: string): string | null =>
  value === null ? null : uuidValue(value, name);

const timestampValue = (value: unknown, name: string): string => {
  const timestamp = stringValue(value, name);
  if (!timestamp.endsWith("Z") || !Number.isFinite(Date.parse(timestamp))) {
    invalid(`${name} must be a UTC timestamp.`);
  }
  return timestamp;
};

const nullableTimestamp = (value: unknown, name: string): string | null =>
  value === null ? null : timestampValue(value, name);

const calendarDateValue = (value: unknown, name: string): string => {
  const timestamp = timestampValue(value, name);
  return timestamp.slice(0, 10);
};

const enumValue = <T extends number>(
  value: unknown,
  allowed: readonly T[],
  name: string,
): T => allowed.includes(value as T) ? value as T : invalid(`${name} is not supported.`);

const baseRecord = (value: Record<string, unknown>, name: string): LegacyBaseRecord => ({
  id: uuidValue(value.id, `${name}.id`),
  legacyId: integerValue(value.legacyId, `${name}.legacyId`),
  isDeleted: booleanValue(value.isDeleted, `${name}.isDeleted`),
  createdAt: timestampValue(value.createdAt, `${name}.createdAt`),
  updatedAt: nullableTimestamp(value.updatedAt, `${name}.updatedAt`),
});

const records = <T>(
  value: unknown,
  name: string,
  parse: (record: Record<string, unknown>, recordName: string) => T,
): T[] => arrayValue(value, name).map((entry, index) =>
  parse(objectValue(entry, `${name}[${index}]`), `${name}[${index}]`));

const parseAccount = (value: Record<string, unknown>, name: string): LegacyAccountRecord => ({
  ...baseRecord(value, name),
  label: stringValue(value.label, `${name}.label`),
  accountNumber: stringValue(value.accountNumber, `${name}.accountNumber`),
  balanceMinorUnits: integerValue(value.balanceMinorUnits, `${name}.balanceMinorUnits`),
  currency: enumValue(value.currency, [0, 1], `${name}.currency`),
  isDefault: booleanValue(value.isDefault, `${name}.isDefault`),
});

const parseNecessityScale = (
  value: Record<string, unknown>,
  name: string,
): LegacyNecessityScaleRecord => ({
  ...baseRecord(value, name),
  label: stringValue(value.label, `${name}.label`),
  weight: finiteNumber(value.weight, `${name}.weight`),
});

const parseCategory = (value: Record<string, unknown>, name: string): LegacyCategoryRecord => ({
  ...baseRecord(value, name),
  label: stringValue(value.label, `${name}.label`),
  necessityScaleLegacyId: integerValue(
    value.necessityScaleLegacyId,
    `${name}.necessityScaleLegacyId`,
  ),
  necessityScaleId: uuidValue(value.necessityScaleId, `${name}.necessityScaleId`),
});

const parseMerchant = (value: Record<string, unknown>, name: string): LegacyMerchantRecord => ({
  ...baseRecord(value, name),
  label: stringValue(value.label, `${name}.label`),
});

const parseTag = (value: Record<string, unknown>, name: string): LegacyTagRecord => ({
  ...baseRecord(value, name),
  label: stringValue(value.label, `${name}.label`),
  backgroundColorHex: nullableString(value.backgroundColorHex, `${name}.backgroundColorHex`),
  foregroundColorHex: nullableString(value.foregroundColorHex, `${name}.foregroundColorHex`),
});

const parseTransaction = (
  value: Record<string, unknown>,
  name: string,
): LegacyTransactionRecord => ({
  ...baseRecord(value, name),
  kind: enumValue(value.kind, [0, 1, 2], `${name}.kind`),
  amountMinorUnits: integerValue(value.amountMinorUnits, `${name}.amountMinorUnits`),
  currency: enumValue(value.currency, [0, 1], `${name}.currency`),
  occurredAt: timestampValue(value.occurredAt, `${name}.occurredAt`),
  reason: nullableString(value.reason, `${name}.reason`),
  sourceAccountLegacyId: nullableInteger(
    value.sourceAccountLegacyId,
    `${name}.sourceAccountLegacyId`,
  ),
  sourceAccountId: nullableUuid(value.sourceAccountId, `${name}.sourceAccountId`),
  destinationAccountLegacyId: nullableInteger(
    value.destinationAccountLegacyId,
    `${name}.destinationAccountLegacyId`,
  ),
  destinationAccountId: nullableUuid(value.destinationAccountId, `${name}.destinationAccountId`),
  categoryLegacyId: nullableInteger(value.categoryLegacyId, `${name}.categoryLegacyId`),
  categoryId: nullableUuid(value.categoryId, `${name}.categoryId`),
  merchantLegacyId: nullableInteger(value.merchantLegacyId, `${name}.merchantLegacyId`),
  merchantId: nullableUuid(value.merchantId, `${name}.merchantId`),
  tagLegacyIds: arrayValue(value.tagLegacyIds, `${name}.tagLegacyIds`).map((id, index) =>
    integerValue(id, `${name}.tagLegacyIds[${index}]`)),
  tagIds: arrayValue(value.tagIds, `${name}.tagIds`).map((id, index) =>
    uuidValue(id, `${name}.tagIds[${index}]`)),
});

const parseBudget = (value: Record<string, unknown>, name: string): LegacyBudgetRecord => ({
  ...baseRecord(value, name),
  amountMinorUnits: integerValue(value.amountMinorUnits, `${name}.amountMinorUnits`),
  currency: enumValue(value.currency, [0, 1], `${name}.currency`),
  categoryLegacyId: integerValue(value.categoryLegacyId, `${name}.categoryLegacyId`),
  categoryId: uuidValue(value.categoryId, `${name}.categoryId`),
  recurrence: enumValue(value.recurrence, [0, 1, 2, 3], `${name}.recurrence`),
  startsOn: calendarDateValue(value.startsOn, `${name}.startsOn`),
  endsOn: value.endsOn === null ? null : calendarDateValue(value.endsOn, `${name}.endsOn`),
  alertThresholdPercent: nullableInteger(
    value.alertThresholdPercent,
    `${name}.alertThresholdPercent`,
  ),
});

const parseNotification = (
  value: Record<string, unknown>,
  name: string,
): LegacyNotificationRecord => ({
  ...baseRecord(value, name),
  ownerLegacyId: nullableInteger(value.ownerLegacyId, `${name}.ownerLegacyId`),
  eventId: uuidValue(value.eventId, `${name}.eventId`),
  kind: integerValue(value.kind, `${name}.kind`),
  title: stringValue(value.title, `${name}.title`),
  message: stringValue(value.message, `${name}.message`),
  notificationPayload: stringValue(value.payload, `${name}.payload`, true),
  payloadHash: stringValue(value.payloadHash, `${name}.payloadHash`),
  readAt: nullableTimestamp(value.readAt, `${name}.readAt`),
});

const currencyName = (currency: 0 | 1): Currency =>
  currency === 0 ? Currency.EUR : Currency.USD;

const recurrenceName = (recurrence: 0 | 1 | 2 | 3): "None" | "Weekly" | "Monthly" | "Yearly" =>
  (["None", "Weekly", "Monthly", "Yearly"] as const)[recurrence];

const transactionKindName = (kind: 0 | 1 | 2): "income" | "expense" | "transfer" =>
  (["income", "expense", "transfer"] as const)[kind];

const priorityName = (weight: number): "Essential" | "Important" | "Useful" | "Optional" | "Avoidable" => {
  if (weight >= 0.9) return "Essential";
  if (weight >= 0.7) return "Important";
  if (weight >= 0.5) return "Useful";
  if (weight >= 0.3) return "Optional";
  return "Avoidable";
};

const assertRelationships = (dataset: LegacyClaimDataset): void => {
  const accounts = new Map(dataset.accounts.map((record) => [record.id, record]));
  const scales = new Map(dataset.necessityScales.map((record) => [record.id, record]));
  const categories = new Map(dataset.categories.map((record) => [record.id, record]));
  const merchants = new Map(dataset.merchants.map((record) => [record.id, record]));
  const tags = new Map(dataset.tags.map((record) => [record.id, record]));

  for (const category of dataset.categories) {
    const scale = scales.get(category.necessityScaleId);
    if (scale?.legacyId !== category.necessityScaleLegacyId) {
      invalid(`Legacy category ${category.id}: The necessity scale must be a valid selection.`);
    }
  }

  for (const transaction of dataset.transactions) {
    const prefix = `Legacy transaction ${transaction.id}:`;
    if (transaction.amountMinorUnits <= 0) invalid(`${prefix} Transaction amount must be positive.`);
    const source = transaction.sourceAccountId === null ? null : accounts.get(transaction.sourceAccountId);
    const destination = transaction.destinationAccountId === null
      ? null
      : accounts.get(transaction.destinationAccountId);
    if ((transaction.sourceAccountId === null) !== (transaction.sourceAccountLegacyId === null) ||
        (source !== null && source !== undefined && source.legacyId !== transaction.sourceAccountLegacyId) ||
        (transaction.destinationAccountId === null) !== (transaction.destinationAccountLegacyId === null) ||
        (destination !== null && destination !== undefined &&
          destination.legacyId !== transaction.destinationAccountLegacyId)) {
      invalid(`${prefix} The account relationship is invalid.`);
    }
    const validShape = transaction.kind === 0
      ? source === null && destination !== undefined && destination !== null
      : transaction.kind === 1
        ? source !== undefined && source !== null && destination === null
        : source !== undefined && source !== null && destination !== undefined && destination !== null && source.id !== destination.id;
    if (!validShape) invalid(`${prefix} The account relationship is invalid.`);
    for (const account of [source, destination]) {
      if (account !== null && account !== undefined && account.currency !== transaction.currency) {
        invalid(`${prefix} The transaction currency must match the account currency.`);
      }
    }
    if ((transaction.categoryId === null) !== (transaction.categoryLegacyId === null) ||
        (transaction.categoryId !== null &&
          categories.get(transaction.categoryId)?.legacyId !== transaction.categoryLegacyId)) {
      invalid(`${prefix} The category must be a valid selection.`);
    }
    if ((transaction.merchantId === null) !== (transaction.merchantLegacyId === null) ||
        (transaction.merchantId !== null &&
          merchants.get(transaction.merchantId)?.legacyId !== transaction.merchantLegacyId)) {
      invalid(`${prefix} The merchant must be a valid selection.`);
    }
    if (transaction.tagIds.length !== transaction.tagLegacyIds.length ||
        transaction.tagIds.some((id, index) => tags.get(id)?.legacyId !== transaction.tagLegacyIds[index])) {
      invalid(`${prefix} Every tag must be a valid selection.`);
    }
    if (transaction.kind === 2 && (transaction.categoryId !== null || transaction.merchantId !== null)) {
      invalid(`${prefix} A transfer cannot have a category or merchant.`);
    }
  }

  for (const budget of dataset.budgets) {
    const prefix = `Legacy budget ${budget.id}:`;
    if (budget.amountMinorUnits <= 0) invalid(`${prefix} A budget amount must be positive.`);
    if (categories.get(budget.categoryId)?.legacyId !== budget.categoryLegacyId) {
      invalid(`${prefix} The category must be a valid selection.`);
    }
    if (budget.alertThresholdPercent !== null &&
        (budget.alertThresholdPercent < 1 || budget.alertThresholdPercent > 100)) {
      invalid(`${prefix} An alert threshold must be between 1 and 100 percent.`);
    }
    try {
      validateBudgetWindow({
        recurrence: recurrenceName(budget.recurrence),
        startsOn: budget.startsOn,
        endsOn: budget.endsOn,
      });
    } catch (error) {
      invalid(`${prefix} ${error instanceof Error ? error.message : "The budget window is invalid."}`);
    }
  }
};

const allIdentities = (dataset: LegacyClaimDataset): ClaimRecordIdentity[] => [
  ...dataset.accounts.map((record) => ({ recordType: "account", id: record.id })),
  ...dataset.necessityScales.map((record) => ({ recordType: "necessityScale", id: record.id })),
  ...dataset.categories.map((record) => ({ recordType: "category", id: record.id })),
  ...dataset.merchants.map((record) => ({ recordType: "merchant", id: record.id })),
  ...dataset.tags.map((record) => ({ recordType: "tag", id: record.id })),
  ...dataset.transactions.map((record) => ({
    recordType: record.kind === 2 ? "transfer" : "transaction",
    id: record.id,
  })),
  ...dataset.budgets.map((record) => ({ recordType: "budget", id: record.id })),
  ...dataset.notifications.map((record) => ({ recordType: "notification", id: record.id })),
];

export const parseClaimDataset = async (value: unknown): Promise<LegacyClaimDataset> => {
  const source = objectValue(value, "The legacy dataset");
  if (source.protocolVersion !== protocolVersion) invalid("The legacy dataset protocol is not supported.");
  const countsSource = objectValue(source.counts, "The legacy dataset counts");
  const counts = Object.fromEntries(requiredCountKeys.map((key) => [
    key,
    integerValue(countsSource[key], `The ${key} count`),
  ])) as LegacyClaimDataset["counts"];
  if (Object.keys(countsSource).length !== requiredCountKeys.length) {
    invalid("The legacy dataset counts are invalid.");
  }
  const parsed: LegacyClaimDataset = {
    protocolVersion,
    recordCount: integerValue(source.recordCount, "The legacy dataset record count"),
    counts,
    manifestHash: stringValue(source.manifestHash, "The legacy dataset manifest hash").toLowerCase(),
    accounts: records(source.accounts, "accounts", parseAccount),
    necessityScales: records(source.necessityScales, "necessityScales", parseNecessityScale),
    categories: records(source.categories, "categories", parseCategory),
    merchants: records(source.merchants, "merchants", parseMerchant),
    tags: records(source.tags, "tags", parseTag),
    transactions: records(source.transactions, "transactions", parseTransaction),
    budgets: records(source.budgets, "budgets", parseBudget),
    notifications: records(source.notifications, "notifications", parseNotification),
  };
  if (!manifestHashPattern.test(parsed.manifestHash)) {
    invalid("The legacy dataset manifest hash is invalid.");
  }
  const identities = allIdentities(parsed);
  if (new Set(identities.map((identity) => identity.id)).size !== identities.length) {
    invalid("The legacy dataset contains a duplicate record UUID.");
  }
  const actualCounts: LegacyClaimDataset["counts"] = {
    account: parsed.accounts.length,
    necessityScale: parsed.necessityScales.length,
    category: parsed.categories.length,
    merchant: parsed.merchants.length,
    tag: parsed.tags.length,
    transaction: parsed.transactions.filter((record) => record.kind !== 2).length,
    transfer: parsed.transactions.filter((record) => record.kind === 2).length,
    budget: parsed.budgets.length,
    notification: parsed.notifications.length,
  };
  if (parsed.recordCount !== identities.length ||
      requiredCountKeys.some((key) => parsed.counts[key] !== actualCounts[key])) {
    invalid("The legacy dataset counts do not match its records.");
  }
  if (await hashClaimManifest(identities) !== parsed.manifestHash) {
    invalid("The legacy dataset manifest does not match its records.");
  }
  assertRelationships(parsed);
  const openings = deriveOpeningBalances(parsed);
  const recomputed = recomputeCurrentBalances(parsed, openings);
  for (const account of parsed.accounts) {
    if (recomputed.get(account.id) !== account.balanceMinorUnits) {
      invalid(`Legacy account ${account.id}: The opening balance could not be verified.`);
    }
  }
  return parsed;
};

export const deriveOpeningBalances = (dataset: LegacyClaimDataset): Map<string, number> => {
  const openings = new Map(dataset.accounts.map((account) => [account.id, account.balanceMinorUnits]));
  const adjust = (accountId: string, delta: number): void => {
    const balance = openings.get(accountId)! + delta;
    if (!Number.isSafeInteger(balance)) {
      invalid(`Legacy account ${accountId}: The opening balance is outside the safe range.`);
    }
    openings.set(accountId, balance);
  };
  for (const transaction of dataset.transactions) {
    if (transaction.isDeleted) continue;
    if (transaction.kind === 0) {
      adjust(transaction.destinationAccountId!, -transaction.amountMinorUnits);
    } else if (transaction.kind === 1) {
      adjust(transaction.sourceAccountId!, transaction.amountMinorUnits);
    } else {
      adjust(transaction.sourceAccountId!, transaction.amountMinorUnits);
      adjust(transaction.destinationAccountId!, -transaction.amountMinorUnits);
    }
  }
  return openings;
};

export const recomputeCurrentBalances = (
  dataset: LegacyClaimDataset,
  openingBalances: ReadonlyMap<string, number>,
): Map<string, number> => {
  const current = new Map(openingBalances);
  const adjust = (accountId: string, delta: number): void => {
    const balance = current.get(accountId)! + delta;
    if (!Number.isSafeInteger(balance)) {
      invalid(`Legacy account ${accountId}: The current balance is outside the safe range.`);
    }
    current.set(accountId, balance);
  };
  for (const transaction of dataset.transactions) {
    if (transaction.isDeleted) continue;
    if (transaction.kind === 0) {
      adjust(transaction.destinationAccountId!, transaction.amountMinorUnits);
    } else if (transaction.kind === 1) {
      adjust(transaction.sourceAccountId!, -transaction.amountMinorUnits);
    } else {
      adjust(transaction.sourceAccountId!, -transaction.amountMinorUnits);
      adjust(transaction.destinationAccountId!, transaction.amountMinorUnits);
    }
  }
  return current;
};

const commonPayload = (record: LegacyBaseRecord): Record<string, unknown> => ({
  schemaVersion: protocolVersion,
  recordId: record.id,
  legacyId: record.legacyId,
  isDeleted: record.isDeleted,
  createdAt: record.createdAt,
  updatedAt: record.updatedAt,
});

export const prepareClaimRecords = (dataset: LegacyClaimDataset): PreparedClaimRecord[] => {
  const openings = deriveOpeningBalances(dataset);
  const scales = new Map(dataset.necessityScales.map((record) => [record.id, record]));
  const categories = new Map(dataset.categories.map((record) => [record.id, record]));
  const merchants = new Map(dataset.merchants.map((record) => [record.id, record]));
  const tags = new Map(dataset.tags.map((record) => [record.id, record]));
  const accounts = new Map(dataset.accounts.map((record) => [record.id, record]));
  const prepared: PreparedClaimRecord[] = [];
  const add = (
    record: LegacyBaseRecord,
    recordType: ClaimRecordType,
    parentResourceId: string | null,
    payload: Record<string, unknown>,
  ): void => {
    prepared.push({
      id: record.id,
      recordType,
      recordTypeCode: recordTypeCodes[recordType],
      parentResourceId,
      tombstone: record.isDeleted,
      createdAt: record.createdAt,
      updatedAt: record.updatedAt,
      payload: { ...commonPayload(record), ...payload },
    });
  };

  for (const account of dataset.accounts) {
    add(account, "account", account.id, {
      accountNumber: account.accountNumber,
      label: account.label,
      balance: { minorUnits: account.balanceMinorUnits, currency: currencyName(account.currency) },
      openingBalanceMinorUnits: openings.get(account.id),
      currency: currencyName(account.currency),
      isDefault: account.isDefault,
    });
  }
  for (const scale of dataset.necessityScales) {
    add(scale, "necessityScale", null, {
      id: scale.legacyId,
      label: scale.label,
      weight: scale.weight,
      priority: priorityName(scale.weight),
    });
  }
  for (const category of dataset.categories) {
    const scale = scales.get(category.necessityScaleId)!;
    add(category, "category", null, {
      id: category.legacyId,
      label: category.label,
      necessityScaleId: category.necessityScaleId,
      priority: {
        id: scale.legacyId,
        recordId: scale.id,
        label: scale.label,
        weight: scale.weight,
        createdAt: scale.createdAt,
        updatedAt: scale.updatedAt,
      },
    });
  }
  for (const merchant of dataset.merchants) {
    add(merchant, "merchant", null, { id: merchant.legacyId, label: merchant.label });
  }
  for (const tag of dataset.tags) {
    add(tag, "tag", null, {
      id: tag.legacyId,
      label: tag.label,
      bgColorHex: tag.backgroundColorHex ?? "",
      fgColorHex: tag.foregroundColorHex ?? "",
      backgroundColorHex: tag.backgroundColorHex,
      foregroundColorHex: tag.foregroundColorHex,
    });
  }
  for (const transaction of dataset.transactions) {
    const source = transaction.sourceAccountId === null ? null : accounts.get(transaction.sourceAccountId)!;
    const destination = transaction.destinationAccountId === null
      ? null
      : accounts.get(transaction.destinationAccountId)!;
    const category = transaction.categoryId === null ? null : categories.get(transaction.categoryId)!;
    const merchant = transaction.merchantId === null ? null : merchants.get(transaction.merchantId)!;
    add(
      transaction,
      transaction.kind === 2 ? "transfer" : "transaction",
      transaction.kind === 0 ? transaction.destinationAccountId : transaction.sourceAccountId,
      {
        id: transaction.legacyId,
        kind: transactionKindName(transaction.kind),
        amount: { minorUnits: transaction.amountMinorUnits, currency: currencyName(transaction.currency) },
        sourceAccountNumber: source?.accountNumber ?? null,
        sourceAccountId: transaction.sourceAccountId,
        destinationAccountNumber: destination?.accountNumber ?? null,
        destinationAccountId: transaction.destinationAccountId,
        categoryId: category?.legacyId ?? null,
        categoryRecordId: transaction.categoryId,
        merchant: merchant === null ? null : { id: merchant.legacyId, label: merchant.label },
        merchantRecordId: transaction.merchantId,
        tags: transaction.tagIds.map((id) => {
          const tag = tags.get(id)!;
          return { id: tag.legacyId, recordId: tag.id, label: tag.label };
        }),
        tagRecordIds: transaction.tagIds,
        reason: transaction.reason,
        occurredAt: transaction.occurredAt,
      },
    );
  }
  for (const budget of dataset.budgets) {
    const category = categories.get(budget.categoryId)!;
    const scale = scales.get(category.necessityScaleId)!;
    add(budget, "budget", budget.id, {
      id: budget.legacyId,
      categoryRecordId: budget.categoryId,
      category: {
        id: category.legacyId,
        label: category.label,
        priority: {
          id: scale.legacyId,
          label: scale.label,
          weight: scale.weight,
          createdAt: scale.createdAt,
          updatedAt: scale.updatedAt,
        },
        createdAt: category.createdAt,
        updatedAt: category.updatedAt,
      },
      amount: { minorUnits: budget.amountMinorUnits, currency: currencyName(budget.currency) },
      recurrence: recurrenceName(budget.recurrence),
      startsOn: budget.startsOn,
      endsOn: budget.endsOn,
      alertThresholdPercent: budget.alertThresholdPercent,
      period: null,
    });
  }
  for (const notification of dataset.notifications) {
    add(notification, "notification", null, {
      id: notification.legacyId,
      ownerLegacyId: notification.ownerLegacyId,
      eventId: notification.eventId,
      kind: notification.kind === 1 ? "BudgetExceeded" : String(notification.kind),
      title: notification.title,
      message: notification.message,
      payload: notification.notificationPayload,
      payloadHash: notification.payloadHash,
      readAt: notification.readAt,
    });
  }
  for (const record of prepared) {
    parseVaultPayloadV1(record.recordType, record.id, record.payload);
  }
  assertPreparedClaimPayloadSizes(prepared);
  return prepared;
};
