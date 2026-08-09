import type {
  IAccountResponse,
  IBudgetResponse,
  IBudgetPeriodResponse,
  ICategoryResponse,
  IMerchantResponse,
  IMoneyResponse,
  INotificationResponse,
  IPriorityResponse,
  ITagResponse,
  ITransactionOptionResponse,
  ITransactionResponse,
  TransactionKind,
} from "../clients/types";
import { Currency } from "../typings/enums/Currency";
import {
  LEGACY_QUARANTINE_REVISION,
  type VaultDatabase,
  type VaultRecord,
} from "../vault/vaultDatabase";
import type { VaultRecordDecryptor } from "./syncClient";

export interface LocalProjection {
  accounts: IAccountResponse[];
  budgets: IBudgetResponse[];
  categories: ICategoryResponse[];
  merchants: IMerchantResponse[];
  notifications: INotificationResponse[];
  tags: ITagResponse[];
  transactions: ITransactionResponse[];
}

const decoder = new TextDecoder();

const emptyProjection = (): LocalProjection => ({
  accounts: [],
  budgets: [],
  categories: [],
  merchants: [],
  notifications: [],
  tags: [],
  transactions: [],
});

const asObject = (bytes: Uint8Array): Record<string, unknown> => {
  const parsed: unknown = JSON.parse(decoder.decode(bytes));
  if (parsed === null || typeof parsed !== "object" || Array.isArray(parsed)) {
    throw new Error("The decrypted record payload is invalid");
  }
  return parsed as Record<string, unknown>;
};

const stringValue = (value: unknown): string => {
  if (typeof value !== "string") throw new Error("The decrypted record payload is invalid");
  return value;
};

const nullableString = (value: unknown): string | null =>
  value === null ? null : stringValue(value);

const numberValue = (value: unknown): number => {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    throw new Error("The decrypted record payload is invalid");
  }
  return value;
};

const nullableNumber = (value: unknown): number | null =>
  value === null ? null : numberValue(value);

const booleanValue = (value: unknown): boolean => {
  if (typeof value !== "boolean") throw new Error("The decrypted record payload is invalid");
  return value;
};

const arrayValue = (value: unknown): unknown[] => {
  if (!Array.isArray(value)) throw new Error("The decrypted record payload is invalid");
  return value;
};

const objectValue = (value: unknown): Record<string, unknown> => {
  if (value === null || typeof value !== "object" || Array.isArray(value)) {
    throw new Error("The decrypted record payload is invalid");
  }
  return value as Record<string, unknown>;
};

const currencyValue = (value: unknown): Currency => {
  if (value !== Currency.EUR && value !== Currency.USD) {
    throw new Error("The decrypted record payload is invalid");
  }
  return value;
};

const moneyValue = (value: unknown): IMoneyResponse => {
  const money = objectValue(value);
  return {
    minorUnits: numberValue(money.minorUnits),
    currency: currencyValue(money.currency),
  };
};

const priorityValue = (value: unknown): IPriorityResponse => {
  const priority = objectValue(value);
  return {
    id: numberValue(priority.id),
    label: stringValue(priority.label),
    weight: numberValue(priority.weight),
    createdAt: stringValue(priority.createdAt),
    updatedAt: nullableString(priority.updatedAt),
  };
};

const categoryValue = (value: unknown): ICategoryResponse => {
  const category = objectValue(value);
  return {
    id: numberValue(category.id),
    label: stringValue(category.label),
    priority: priorityValue(category.priority),
    createdAt: stringValue(category.createdAt),
    updatedAt: nullableString(category.updatedAt),
  };
};

const accountValue = (value: unknown): IAccountResponse => {
  const account = objectValue(value);
  return {
    accountNumber: stringValue(account.accountNumber),
    label: stringValue(account.label),
    balance: moneyValue(account.balance),
    isDefault: booleanValue(account.isDefault),
    createdAt: stringValue(account.createdAt),
    updatedAt: nullableString(account.updatedAt),
  };
};

const merchantValue = (value: unknown): IMerchantResponse => {
  const merchant = objectValue(value);
  return {
    id: numberValue(merchant.id),
    label: stringValue(merchant.label),
    createdAt: stringValue(merchant.createdAt),
    updatedAt: nullableString(merchant.updatedAt),
  };
};

const tagValue = (value: unknown): ITagResponse => {
  const tag = objectValue(value);
  return {
    id: numberValue(tag.id),
    label: stringValue(tag.label),
    bgColorHex: stringValue(tag.bgColorHex),
    fgColorHex: stringValue(tag.fgColorHex),
    createdAt: stringValue(tag.createdAt),
    updatedAt: nullableString(tag.updatedAt),
  };
};

const optionValue = (value: unknown): ITransactionOptionResponse => {
  const option = objectValue(value);
  return { id: numberValue(option.id), label: stringValue(option.label) };
};

const transactionKindValue = (value: unknown): TransactionKind => {
  if (value !== "income" && value !== "expense" && value !== "transfer") {
    throw new Error("The decrypted record payload is invalid");
  }
  return value;
};

const transactionValue = (value: unknown): ITransactionResponse => {
  const transaction = objectValue(value);
  return {
    id: numberValue(transaction.id),
    kind: transactionKindValue(transaction.kind),
    amount: moneyValue(transaction.amount),
    sourceAccountNumber: nullableString(transaction.sourceAccountNumber),
    destinationAccountNumber: nullableString(transaction.destinationAccountNumber),
    categoryId: nullableNumber(transaction.categoryId),
    merchant: transaction.merchant === null ? null : optionValue(transaction.merchant),
    tags: arrayValue(transaction.tags).map(optionValue),
    reason: nullableString(transaction.reason),
    occurredAt: stringValue(transaction.occurredAt),
    createdAt: stringValue(transaction.createdAt),
    updatedAt: nullableString(transaction.updatedAt),
  };
};

const budgetPeriodValue = (value: unknown): IBudgetPeriodResponse => {
  const period = objectValue(value);
  return {
    name: stringValue(period.name),
    from: stringValue(period.from),
    toExclusive: stringValue(period.toExclusive),
    spent: moneyValue(period.spent),
    remaining: moneyValue(period.remaining),
    exceeded: booleanValue(period.exceeded),
    uncounted: arrayValue(period.uncounted).map(moneyValue),
  };
};

const budgetValue = (value: unknown): IBudgetResponse => {
  const budget = objectValue(value);
  return {
    id: numberValue(budget.id),
    category: categoryValue(budget.category),
    amount: moneyValue(budget.amount),
    recurrence: stringValue(budget.recurrence),
    startsOn: stringValue(budget.startsOn),
    endsOn: nullableString(budget.endsOn),
    alertThresholdPercent: nullableNumber(budget.alertThresholdPercent),
    period: budget.period === null ? null : budgetPeriodValue(budget.period),
    createdAt: stringValue(budget.createdAt),
    updatedAt: nullableString(budget.updatedAt),
  };
};

const notificationValue = (value: unknown): INotificationResponse => {
  const notification = objectValue(value);
  return {
    id: numberValue(notification.id),
    kind: stringValue(notification.kind),
    title: stringValue(notification.title),
    message: stringValue(notification.message),
    payload: notification.payload,
    readAt: nullableString(notification.readAt),
    createdAt: stringValue(notification.createdAt),
  };
};

const append = (
  projection: LocalProjection,
  record: VaultRecord,
  payload: Record<string, unknown>,
): void => {
  switch (record.recordType) {
    case "account":
      projection.accounts.push(accountValue(payload));
      return;
    case "budget":
      projection.budgets.push(budgetValue(payload));
      return;
    case "category":
      projection.categories.push(categoryValue(payload));
      return;
    case "merchant":
      projection.merchants.push(merchantValue(payload));
      return;
    case "notification":
      projection.notifications.push(notificationValue(payload));
      return;
    case "tag":
      projection.tags.push(tagValue(payload));
      return;
    case "transaction":
    case "transfer":
      projection.transactions.push(transactionValue(payload));
      return;
    case "userProfile":
    case "necessityScale":
      return;
  }
};

export const rebuildProjection = async (
  database: VaultDatabase,
  decryptor: VaultRecordDecryptor,
): Promise<LocalProjection> => {
  const [records, quarantineEntries] = await Promise.all([
    database.records(),
    database.quarantineEntries(),
  ]);
  const quarantineKey = (recordId: string, revision: number): string =>
    `${recordId}:${revision}`;
  const quarantined = new Set(
    quarantineEntries.map((entry) => quarantineKey(entry.recordId, entry.revision)),
  );
  const legacyQuarantined = new Set(
    quarantineEntries
      .filter((entry) => entry.revision === LEGACY_QUARANTINE_REVISION)
      .map((entry) => entry.recordId),
  );
  const projection = emptyProjection();

  for (const record of records) {
    if (
      record.tombstone ||
      legacyQuarantined.has(record.id) ||
      quarantined.has(quarantineKey(record.id, record.revision))
    ) continue;
    let decrypted: Uint8Array;
    try {
      decrypted = await decryptor.decrypt(record);
    } catch {
      await database.putQuarantine({
        recordId: record.id,
        revision: record.revision,
        record,
        reason: "authentication-failed",
      });
      continue;
    }
    try {
      append(projection, record, asObject(decrypted));
    } catch {
      await database.putQuarantine({
        recordId: record.id,
        revision: record.revision,
        record,
        reason: "invalid-payload",
      });
      continue;
    }
  }
  return projection;
};
