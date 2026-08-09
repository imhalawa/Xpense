import { createAccount, deleteAccount, listAccounts, updateAccount } from "../clients/accounts";
import { createCategory, deleteCategory, listCategories, updateCategory } from "../clients/categories";
import { createMerchant, deleteMerchant, listMerchants, updateMerchant } from "../clients/merchants";
import {
  createBudget,
  deleteBudget,
  listBudgets,
  updateBudget,
} from "../clients/budgets";
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
  BudgetDraft,
  BudgetView,
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
import { validateTransactionCurrencies } from "../domain/currencyMatch";
import { budgetSpending } from "../domain/budgetSpending";

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
  balanceSource: "current",
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
  let legacyCategoryIds = new Map<string, number>();
  let legacyTaxonomyIds = new Map<string, number>();
  const legacyBudgetIds = new Map<string, number>();
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
    legacyCategoryIds = new Map(categories.map((category) => [String(category.id), category.id]));
    legacyTaxonomyIds = new Map([
      ...categories.map((category) => [String(category.id), category.id] as const),
      ...merchants.map((merchant) => [String(merchant.id), merchant.id] as const),
      ...tags.map((tag) => [String(tag.id), tag.id] as const),
    ]);
    priorities = loadedPriorities;

    const seed: FixtureSeed = {
      spaces: [personalSpace],
      accounts: { [personalSpaceId]: accounts.map(toAccountView) },
      taxonomy: { [personalSpaceId]: taxonomy },
      transactions: { [personalSpaceId]: ledger.rows.map(toTransactionView) },
      transactionHistoryComplete: { [personalSpaceId]: ledger.rows.length >= ledger.availableRowCount },
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
      : { id: legacyTaxonomyIds.get(known.id) ?? null, label, create: false };
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
    categoryId: draft.categoryId === null ? null : legacyTaxonomyIds.get(draft.categoryId) ?? null,
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
      legacyCategoryIds.clear();
      legacyTaxonomyIds.clear();
      legacyBudgetIds.clear();
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

    async listAccountBalances(space) {
      return requireLoaded().listAccountBalances(space);
    },

    async listTaxonomy(space, kind) {
      return requireLoaded().listTaxonomy(space, kind);
    },

    async listBudgets(space, on) {
      requireLoaded();
      requirePersonalSpace(space);
      const [budgets, accounts, transactions] = await Promise.all([
        listBudgets(null),
        requireLoaded().listAccounts(space),
        requireLoaded().queryTransactions(
          { space, category: null, merchant: null, tag: null, account: null, from: null, to: null },
          { offset: 0, limit: transactionRowCeiling },
        ),
      ]);
      return budgets.map((budget) => {
        const budgetId = String(budget.id);
        const categoryId = String(budget.category.id);
        legacyBudgetIds.set(budgetId, budget.id);
        const spending = budgetSpending(
          {
            spaceId: space,
            categoryId,
            amount: budget.amount,
            recurrence: budget.recurrence as "None" | "Weekly" | "Monthly" | "Yearly",
            startsOn: budget.startsOn,
            endsOn: budget.endsOn,
          },
          transactions.rows.map((transaction) => ({
            kind: transaction.kind,
            categoryId: transaction.categoryId,
            accountId: transaction.accountId,
            amount: { minorUnits: transaction.amountMinorUnits, currency: transaction.currency },
            occurredAt: transaction.occurredAt,
          })),
          accounts.map((account) => ({ id: account.id, spaceId: space })),
          on,
        );
        return {
          id: budgetId,
          category: { id: categoryId, label: budget.category.label },
          amount: budget.amount,
          recurrence: budget.recurrence as "None" | "Weekly" | "Monthly" | "Yearly",
          startsOn: budget.startsOn,
          endsOn: budget.endsOn,
          alertThresholdPercent: budget.alertThresholdPercent,
          period: spending.period === null
            ? null
            : { ...spending.period, spent: spending.spent, remaining: spending.remaining, exceeded: spending.exceeded, uncounted: spending.uncounted },
          createdAt: budget.createdAt,
          updatedAt: budget.updatedAt,
          canEdit: true,
        };
      });
    },

    async saveBudget(space: string, draft: BudgetDraft): Promise<BudgetView> {
      requireLoaded();
      requirePersonalSpace(space);
      const categoryId = legacyCategoryIds.get(draft.categoryId);
      if (categoryId === undefined) throw new Error("The category must be a valid selection.");
      const request = {
        amount: draft.amount,
        recurrence: draft.recurrence,
        startsOn: draft.startsOn,
        endsOn: draft.endsOn,
        alertThresholdPercent: draft.alertThresholdPercent,
      };
      const saved = draft.id === null
        ? await createBudget({ categoryId, ...request })
        : await updateBudget(
            (() => {
              const id = legacyBudgetIds.get(draft.id!);
              if (id === undefined) throw new Error("The budget was not found.");
              return id;
            })(),
            request,
          );
      await load();
      legacyBudgetIds.set(String(saved.id), saved.id);
      return {
        id: String(saved.id),
        category: { id: String(saved.category.id), label: saved.category.label },
        amount: saved.amount,
        recurrence: saved.recurrence as "None" | "Weekly" | "Monthly" | "Yearly",
        startsOn: saved.startsOn,
        endsOn: saved.endsOn,
        alertThresholdPercent: saved.alertThresholdPercent,
        period: null,
        createdAt: saved.createdAt,
        updatedAt: saved.updatedAt,
        canEdit: true,
      };
    },

    async deleteBudget(space: string, id: string): Promise<void> {
      requireLoaded();
      requirePersonalSpace(space);
      const legacyId = legacyBudgetIds.get(id);
      if (legacyId === undefined) throw new Error("The budget was not found.");
      await deleteBudget(legacyId);
      await load();
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

    async listTransactions(space) {
      return requireLoaded().listTransactions(space);
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
      validateTransactionCurrencies(
        draft,
        (await requireLoaded().listAccounts(draft.space)).map((account) => ({
          id: account.id,
          currency: account.currency,
        })),
      );

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
