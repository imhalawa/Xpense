import { createAccount, deleteAccount, listAccounts, updateAccount } from "../clients/accounts";
import { createCategory, deleteCategory, listCategories, updateCategory } from "../clients/categories";
import { createMerchant, deleteMerchant, listMerchants, updateMerchant } from "../clients/merchants";
import { listPriorities } from "../clients/options";
import { createTag, deleteTag, listTags, updateTag } from "../clients/tags";
import {
  createTransaction,
  deleteTransaction,
  listTransactions,
  updateTransaction,
} from "../clients/transactions";
import { fixtureProjection } from "./fixtureProjection";
import type { FixtureSeed } from "./fixtureProjection";
import type {
  IAccountResponse,
  ICategoryResponse,
  ICreateTransactionRequest,
  IMerchantResponse,
  IOptionRequest,
  IPriorityResponse,
  ITagResponse,
  ITransactionResponse,
} from "../clients/types";
import type {
  AccountView,
  CategoryCreationPriority,
  CategoryPriority,
  SpaceSummary,
  TaxonomyKind,
  TaxonomyValue,
  TransactionDraft,
  TransactionView,
  VaultProjection,
  VaultState,
} from "./VaultProjection";

const personalSpaceId = "personal";
const transactionPageSize = 200;
const transactionRowCeiling = 2000;
const lockedMessage = "The vault is locked";
const missingTransferAccountMessage = "Select a destination account before saving the transfer";
const priorityLabelByCreationPriority: Record<CategoryCreationPriority, CategoryPriority> = {
  Essential: "Essential",
  Important: "Important",
  Useful: "Useful",
  Optional: "Optional",
  Avoidable: "Avoidable",
  High: "Important",
  Medium: "Useful",
  Low: "Optional",
};
const categoryPriorities: CategoryPriority[] = [
  "Essential",
  "Important",
  "Useful",
  "Optional",
  "Avoidable",
];

const personalSpace: SpaceSummary = {
  id: personalSpaceId,
  name: "Personal",
  kind: "personal",
  canEdit: true,
};

const categoryPriorityOf = (label: string): CategoryPriority =>
  categoryPriorities.find((priority) => priority === label) ?? "Useful";

export interface RowCeilingReport {
  loadedRowCount: number;
  availableRowCount: number;
  reachedCeiling: boolean;
}

export interface RowCeilingAware {
  readonly rowCeiling: RowCeilingReport | null;
}

export type PlaintextProjection = VaultProjection & RowCeilingAware;

const toAccountView = (account: IAccountResponse): AccountView => ({
  id: String(account.accountNumber),
  label: account.label,
  currency: account.balance.currency,
  canEdit: true,
  balanceMinorUnits: account.balance.minorUnits,
  isDefault: account.isDefault,
});

const toCategoryValue = (category: ICategoryResponse): TaxonomyValue => ({
  id: String(category.id),
  kind: "category",
  label: category.label,
  foregroundHex: null,
  backgroundHex: null,
  canEdit: true,
  priority: categoryPriorityOf(category.priority.label),
});

const toMerchantValue = (merchant: IMerchantResponse): TaxonomyValue => ({
  id: String(merchant.id),
  kind: "merchant",
  label: merchant.label,
  foregroundHex: null,
  backgroundHex: null,
  canEdit: true,
});

const toTagValue = (tag: ITagResponse): TaxonomyValue => ({
  id: String(tag.id),
  kind: "tag",
  label: tag.label,
  foregroundHex: tag.fgColorHex,
  backgroundHex: tag.bgColorHex,
  canEdit: true,
});

const accountSides = (
  transaction: ITransactionResponse,
): { accountId: string | null; counterpartyAccountId: string | null } =>
  transaction.kind === "income"
    ? {
        accountId: transaction.destinationAccountNumber,
        counterpartyAccountId: transaction.sourceAccountNumber,
      }
    : {
        accountId: transaction.sourceAccountNumber,
        counterpartyAccountId: transaction.destinationAccountNumber,
      };

const toTransactionView = (transaction: ITransactionResponse): TransactionView => ({
  id: String(transaction.id),
  kind: transaction.kind,
  amountMinorUnits: transaction.amount.minorUnits,
  currency: transaction.amount.currency,
  occurredAt: transaction.occurredAt,
  ...accountSides(transaction),
  isCounterpartyPrivate: false,
  categoryId: transaction.categoryId === null ? null : String(transaction.categoryId),
  merchantId: transaction.merchant === null ? null : String(transaction.merchant.id),
  tagIds: transaction.tags.map((tag) => String(tag.id)),
  reason: transaction.reason,
  canEdit: true,
});

interface LoadedLedger {
  rows: ITransactionResponse[];
  availableRowCount: number;
}

const loadLedger = async (): Promise<LoadedLedger> => {
  const rows: ITransactionResponse[] = [];
  let availableRowCount = 0;
  let page = 1;
  let totalPages = 1;

  while (page <= totalPages && rows.length < transactionRowCeiling) {
    const response = await listTransactions({ page, pageSize: transactionPageSize });
    rows.push(...response.items);
    availableRowCount = response.totalItems;
    totalPages = response.totalPages;
    page += 1;
  }

  return { rows: rows.slice(0, transactionRowCeiling), availableRowCount };
};

export const plaintextProjection = (): PlaintextProjection => {
  let currentState: VaultState = "locked";
  let loaded: VaultProjection | null = null;
  let taxonomy: TaxonomyValue[] = [];
  let priorities: IPriorityResponse[] = [];
  let ceiling: RowCeilingReport | null = null;
  const listeners = new Set<(state: VaultState) => void>();

  const moveTo = (state: VaultState): void => {
    currentState = state;
    for (const listener of listeners) listener(state);
  };

  const requireLoaded = (): VaultProjection => {
    if (loaded === null) throw new Error(lockedMessage);
    return loaded;
  };

  const load = async (): Promise<void> => {
    const [accounts, categories, merchants, tags, loadedPriorities] = await Promise.all([
      listAccounts(),
      listCategories(),
      listMerchants(),
      listTags(),
      listPriorities(),
    ]);
    const ledger = await loadLedger();

    taxonomy = [
      ...categories.map(toCategoryValue),
      ...merchants.map(toMerchantValue),
      ...tags.map(toTagValue),
    ];
    priorities = loadedPriorities;

    const seed: FixtureSeed = {
      spaces: [personalSpace],
      accounts: { [personalSpaceId]: accounts.map(toAccountView) },
      taxonomy: { [personalSpaceId]: taxonomy },
      transactions: { [personalSpaceId]: ledger.rows.map(toTransactionView) },
    };

    ceiling = {
      loadedRowCount: ledger.rows.length,
      availableRowCount: ledger.availableRowCount,
      reachedCeiling: ledger.rows.length < ledger.availableRowCount,
    };
    loaded = fixtureProjection(seed);
  };

  const optionRequest = (kind: TaxonomyKind, label: string): IOptionRequest => {
    const known = taxonomy.find((value) => value.kind === kind && value.label === label);
    return known === undefined
      ? { id: null, label, create: true }
      : { id: Number(known.id), label, create: false };
  };

  const toCreateRequest = (draft: TransactionDraft): ICreateTransactionRequest => ({
    amount: { minorUnits: draft.amountMinorUnits, currency: draft.currency },
    sourceAccountNumber: draft.kind === "income" ? null : draft.accountId,
    destinationAccountNumber:
      draft.kind === "income"
        ? draft.accountId
        : draft.kind === "transfer"
          ? draft.counterpartyAccountId ?? null
          : null,
    categoryId: draft.categoryId === null ? null : Number(draft.categoryId),
    merchant: draft.merchantLabel === null ? null : optionRequest("merchant", draft.merchantLabel),
    tags: draft.tagLabels.map((label) => optionRequest("tag", label)),
    reason: draft.reason,
    occurredAt: draft.occurredAt,
  });

  const requirePersonalSpace = (space: string): void => {
    if (space !== personalSpaceId) throw new Error("The space was not found");
  };

  const priorityId = (priority: CategoryCreationPriority | undefined): number => {
    const label = priorityLabelByCreationPriority[priority ?? "Useful"];
    const found = priorities.find((item) => item.label === label);
    if (found === undefined) throw new Error("The category priority is unavailable");
    return found.id;
  };

  return {
    get state() {
      return currentState;
    },

    get rowCeiling() {
      return ceiling;
    },

    subscribe(listener) {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },

    async unlock() {
      moveTo("loading");
      try {
        await load();
      } catch (loadFailure) {
        loaded = null;
        ceiling = null;
        moveTo("error");
        throw loadFailure;
      }
      moveTo("ready");
    },

    lock() {
      loaded = null;
      taxonomy = [];
      priorities = [];
      ceiling = null;
      moveTo("locked");
    },

    async listSpaces() {
      requireLoaded();
      return [personalSpace];
    },

    async listAccounts(space) {
      return requireLoaded().listAccounts(space);
    },

    async listTaxonomy(space, kind) {
      return requireLoaded().listTaxonomy(space, kind);
    },

    async createCategory(space, label, priority) {
      requireLoaded();
      requirePersonalSpace(space);
      const created = await createCategory({ label, priorityId: priorityId(priority) });
      await load();
      return toCategoryValue(created);
    },

    async createAccount(space, draft) {
      requireLoaded();
      requirePersonalSpace(space);
      const created = await createAccount({
        label: draft.label,
        balance: { minorUnits: draft.openingBalanceMinorUnits, currency: draft.currency },
      });
      await load();
      if (!draft.isDefault) return toAccountView(created);
      const updated = await updateAccount(created.accountNumber, { label: created.label, isDefault: true });
      await load();
      return toAccountView(updated);
    },

    async updateAccount(space, id, draft) {
      requireLoaded();
      requirePersonalSpace(space);
      const updated = await updateAccount(id, { label: draft.label, isDefault: draft.isDefault });
      await load();
      return toAccountView(updated);
    },

    async deleteAccount(space, id) {
      requireLoaded();
      requirePersonalSpace(space);
      await deleteAccount(id);
      await load();
    },

    async createTaxonomy(space, kind, draft) {
      requireLoaded();
      requirePersonalSpace(space);
      const created =
        kind === "category"
          ? toCategoryValue(await createCategory({ label: draft.label, priorityId: priorityId(draft.priority) }))
          : kind === "tag"
            ? toTagValue(await createTag({
                label: draft.label,
                bgColorHex: draft.backgroundHex ?? "#EDEDED",
                fgColorHex: draft.foregroundHex ?? "#242424",
              }))
            : toMerchantValue(await createMerchant({ label: draft.label }));
      await load();
      return created;
    },

    async updateTaxonomy(space, kind, id, draft) {
      requireLoaded();
      requirePersonalSpace(space);
      const existingPriority = taxonomy.find(
        (value) => value.kind === "category" && value.id === id,
      )?.priority;
      const updated =
        kind === "category"
          ? toCategoryValue(await updateCategory(id, {
              label: draft.label,
              priorityId: priorityId(draft.priority ?? existingPriority),
            }))
          : kind === "tag"
            ? toTagValue(await updateTag(id, {
                label: draft.label,
                bgColorHex: draft.backgroundHex ?? "#EDEDED",
                fgColorHex: draft.foregroundHex ?? "#242424",
              }))
            : toMerchantValue(await updateMerchant(id, { label: draft.label }));
      await load();
      return updated;
    },

    async deleteTaxonomy(space, kind, id) {
      requireLoaded();
      requirePersonalSpace(space);
      if (kind === "category") await deleteCategory(id);
      else if (kind === "tag") await deleteTag(id);
      else await deleteMerchant(id);
      await load();
    },

    async resolveFilter(filter) {
      return requireLoaded().resolveFilter(filter);
    },

    async queryTransactions(filter, page) {
      return requireLoaded().queryTransactions(filter, page);
    },

    async getTransaction(space, id) {
      return requireLoaded().getTransaction(space, id);
    },

    async saveTransaction(draft) {
      requireLoaded();
      if (
        draft.kind === "transfer" &&
        (draft.counterpartyAccountId === null || draft.counterpartyAccountId === undefined)
      ) {
        throw new Error(missingTransferAccountMessage);
      }

      const saved =
        draft.id === null
          ? await createTransaction(toCreateRequest(draft))
          : await updateTransaction(draft.id, toCreateRequest(draft));
      await load();
      return toTransactionView(saved);
    },

    async deleteTransaction(space, id) {
      requireLoaded();
      if (space !== personalSpaceId) throw new Error("The transaction was not found");
      await deleteTransaction(id);
      await load();
    },
  };
};
