import type {
  AccountView,
  AccountBalanceProjection,
  BudgetDraft,
  BudgetView,
  CategoryCreationPriority,
  FilterFacet,
  FilterResolution,
  PageRequest,
  RecordId,
  SpaceId,
  SpaceSummary,
  TaxonomyKind,
  TaxonomyValue,
  TransactionDraft,
  TransactionFilter,
  TransactionPage,
  TransactionView,
  VaultProjection,
  VaultState,
} from "./VaultProjection";
import { budgetSpending, validateBudgetWindow } from "../domain/budgetSpending";
import { accountBalances } from "../domain/accountBalance";
import { validateTransactionCurrencies } from "../domain/currencyMatch";

const millisecondsPerDay = 24 * 60 * 60 * 1000;
const lockedMessage = "The vault is locked";
const priorityAliases: Record<CategoryCreationPriority, "Essential" | "Important" | "Useful" | "Optional" | "Avoidable"> = {
  Essential: "Essential",
  Important: "Important",
  Useful: "Useful",
  Optional: "Optional",
  Avoidable: "Avoidable",
  High: "Important",
  Medium: "Useful",
  Low: "Optional",
};

export interface FixtureSeed {
  spaces: SpaceSummary[];
  accounts: Record<SpaceId, AccountView[]>;
  taxonomy: Record<SpaceId, TaxonomyValue[]>;
  transactions: Record<SpaceId, TransactionView[]>;
  budgets?: Record<SpaceId, BudgetView[]>;
  transactionHistoryComplete?: Record<SpaceId, boolean>;
}

const startOfDay = (calendarDate: string): number => Date.parse(`${calendarDate}T00:00:00.000Z`);

const matchesFilter = (transaction: TransactionView, filter: TransactionFilter): boolean => {
  if (filter.category !== null && transaction.categoryId !== filter.category) return false;
  if (filter.merchant !== null && transaction.merchantId !== filter.merchant) return false;
  if (filter.tag !== null && !transaction.tagIds.includes(filter.tag)) return false;
  if (
    filter.account !== null &&
    transaction.accountId !== filter.account &&
    transaction.counterpartyAccountId !== filter.account
  ) {
    return false;
  }

  const occurredAt = Date.parse(transaction.occurredAt);
  if (filter.from !== null && occurredAt < startOfDay(filter.from)) return false;
  if (filter.to !== null && occurredAt >= startOfDay(filter.to) + millisecondsPerDay) return false;

  return true;
};

export const fixtureProjection = (seed: FixtureSeed): VaultProjection => {
  const transactionsBySpace: Record<SpaceId, TransactionView[]> = {};
  const taxonomyBySpace: Record<SpaceId, TaxonomyValue[]> = {};
  const accountsBySpace: Record<SpaceId, AccountView[]> = {};
  const budgetsBySpace: Record<SpaceId, BudgetView[]> = {};
  for (const [space, transactions] of Object.entries(seed.transactions)) {
    transactionsBySpace[space] = [...transactions];
  }
  for (const [space, values] of Object.entries(seed.taxonomy)) {
    taxonomyBySpace[space] = [...values];
  }
  for (const [space, accounts] of Object.entries(seed.accounts)) {
    accountsBySpace[space] = [...accounts];
  }
  for (const [space, budgets] of Object.entries(seed.budgets ?? {})) {
    budgetsBySpace[space] = [...budgets];
  }

  let currentState: VaultState = "ready";
  let savedTransactionCount = 0;
  let savedBudgetCount = 0;
  const listeners = new Set<(state: VaultState) => void>();

  const moveTo = (state: VaultState): void => {
    currentState = state;
    for (const listener of listeners) listener(state);
  };

  const requireUnlocked = (): void => {
    if (currentState === "locked") throw new Error(lockedMessage);
  };

  const taxonomyOf = (space: SpaceId): TaxonomyValue[] => taxonomyBySpace[space] ?? [];

  const knowsTaxonomy = (space: SpaceId, kind: TaxonomyKind, id: RecordId): boolean =>
    taxonomyOf(space).some((value) => value.kind === kind && value.id === id);

  const knowsAccount = (space: SpaceId, id: RecordId): boolean =>
    (accountsBySpace[space] ?? []).some((account) => account.id === id);

  const findTaxonomyIdByLabel = (
    space: SpaceId,
    kind: TaxonomyKind,
    label: string,
  ): RecordId | null =>
    taxonomyOf(space).find((value) => value.kind === kind && value.label === label)?.id ?? null;

  return {
    get state() {
      return currentState;
    },

    subscribe(listener) {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },

    async unlock() {
      moveTo("ready");
    },

    lock() {
      moveTo("locked");
    },

    async listSpaces(): Promise<SpaceSummary[]> {
      requireUnlocked();
      return seed.spaces;
    },

    async listAccounts(space: SpaceId): Promise<AccountView[]> {
      requireUnlocked();
      return accountsBySpace[space] ?? [];
    },

    async listAccountBalances(space: SpaceId): Promise<AccountBalanceProjection> {
      requireUnlocked();
      const accounts = accountsBySpace[space] ?? [];
      if (accounts.length === 0) return { state: "available", balances: [] };

      let balanceSource: AccountView["balanceSource"];
      const balancesByCurrency = new Map<AccountView["currency"], number>();
      for (const account of accounts) {
        if (account.balanceSource === undefined) {
          return { state: "unavailable", reason: "Balances are unavailable." };
        }
        if (balanceSource !== undefined && balanceSource !== account.balanceSource) {
          return { state: "unavailable", reason: "Balances are unavailable." };
        }
        balanceSource = account.balanceSource;
        if (balanceSource === "current") {
          if (account.balanceMinorUnits === undefined) {
            return { state: "unavailable", reason: "Balances are unavailable." };
          }
          balancesByCurrency.set(
            account.currency,
            (balancesByCurrency.get(account.currency) ?? 0) + account.balanceMinorUnits,
          );
        } else if (account.openingBalanceMinorUnits === undefined) {
          return { state: "unavailable", reason: "Balances are unavailable." };
        }
      }

      if (balanceSource === "current") {
        return {
          state: "available",
          balances: Array.from(balancesByCurrency, ([currency, minorUnits]) => ({ currency, minorUnits })),
        };
      }

      if (seed.transactionHistoryComplete?.[space] !== true) {
        return {
          state: "unavailable",
          reason: "Balances are unavailable because transaction history is incomplete.",
        };
      }

      return {
        state: "available",
        balances: accountBalances(
          accounts,
          (transactionsBySpace[space] ?? []).flatMap((transaction) =>
            transaction.accountId === null
              ? []
              : [{
                  kind: transaction.kind,
                  accountId: transaction.accountId,
                  counterpartyAccountId: transaction.counterpartyAccountId,
                  minorUnits: transaction.amountMinorUnits,
                  currency: transaction.currency,
                }],
          ),
        ),
      };
    },

    async listTaxonomy(space: SpaceId, kind: TaxonomyKind): Promise<TaxonomyValue[]> {
      requireUnlocked();
      return taxonomyOf(space).filter((value) => value.kind === kind);
    },

    async listBudgets(space: SpaceId, on: Date): Promise<BudgetView[]> {
      requireUnlocked();
      const accounts = (accountsBySpace[space] ?? []).map((account) => ({ id: account.id, spaceId: space }));
      const transactions = transactionsBySpace[space] ?? [];
      return (budgetsBySpace[space] ?? []).map((budget) => {
        const spending = budgetSpending(
          {
            spaceId: space,
            categoryId: budget.category.id,
            amount: budget.amount,
            recurrence: budget.recurrence as "None" | "Weekly" | "Monthly" | "Yearly",
            startsOn: budget.startsOn,
            endsOn: budget.endsOn,
          },
          transactions.map((transaction) => ({
            kind: transaction.kind,
            categoryId: transaction.categoryId,
            accountId: transaction.accountId,
            amount: { minorUnits: transaction.amountMinorUnits, currency: transaction.currency },
            occurredAt: transaction.occurredAt,
          })),
          accounts,
          on,
        );
        return {
          ...budget,
          period: spending.period === null
            ? null
            : { ...spending.period, spent: spending.spent, remaining: spending.remaining, exceeded: spending.exceeded, uncounted: spending.uncounted },
        };
      });
    },

    async saveBudget(space: SpaceId, draft: BudgetDraft): Promise<BudgetView> {
      requireUnlocked();
      const category = taxonomyOf(space).find(
        (value) => value.kind === "category" && value.id === draft.categoryId,
      );
      if (category === undefined) throw new Error("The category must be a valid selection.");
      if (draft.amount.minorUnits <= 0) throw new Error("A budget amount must be positive.");
      validateBudgetWindow(draft);
      const budgets = budgetsBySpace[space] ?? [];
      const current = draft.id === null
        ? null
        : budgets.find((budget) => budget.id === draft.id) ?? null;
      if (draft.id !== null && current === null) throw new Error("The budget was not found.");
      if (current?.canEdit === false) throw new Error("The budget cannot be edited.");
      savedBudgetCount += 1;
      const saved: BudgetView = {
        id: draft.id ?? `fixture-budget-${savedBudgetCount}`,
        category: {
          id: draft.categoryId,
          label: category.label,
        },
        amount: draft.amount,
        recurrence: draft.recurrence,
        startsOn: draft.startsOn,
        endsOn: draft.endsOn,
        alertThresholdPercent: draft.alertThresholdPercent,
        period: null,
        createdAt: current?.createdAt ?? new Date().toISOString(),
        updatedAt: current === null ? null : new Date().toISOString(),
        canEdit: current?.canEdit ?? true,
      };
      budgetsBySpace[space] = current === null
        ? [...budgets, saved]
        : budgets.map((budget) => budget.id === saved.id ? saved : budget);
      return saved;
    },

    async deleteBudget(space: SpaceId, id: RecordId): Promise<void> {
      requireUnlocked();
      const budgets = budgetsBySpace[space] ?? [];
      const budget = budgets.find((item) => item.id === id);
      if (budget === undefined) throw new Error("The budget was not found.");
      if (!budget.canEdit) throw new Error("The budget cannot be edited.");
      budgetsBySpace[space] = budgets.filter((budget) => budget.id !== id);
    },

    async createCategory(
      space: SpaceId,
      label: string,
      priority: CategoryCreationPriority,
    ): Promise<TaxonomyValue> {
      requireUnlocked();
      const categories = taxonomyOf(space).filter((value) => value.kind === "category");
      const created: TaxonomyValue = {
        id: `fixture-category-${categories.length + 1}`,
        kind: "category",
        label,
        foregroundHex: null,
        backgroundHex: null,
        priority: priorityAliases[priority],
      };
      taxonomyBySpace[space] = [...taxonomyOf(space), created];
      return created;
    },

    async createAccount(space, draft) {
      requireUnlocked();
      const accounts = accountsBySpace[space] ?? [];
      const created: AccountView = {
        id: `fixture-account-${accounts.length + 1}`,
        label: draft.label,
        currency: draft.currency,
        canEdit: true,
        balanceSource: "opening",
        openingBalanceMinorUnits: draft.openingBalanceMinorUnits,
        isDefault: draft.isDefault,
      };
      accountsBySpace[space] = [...accounts.map((account) => ({ ...account, isDefault: draft.isDefault ? false : account.isDefault })), created];
      return created;
    },

    async updateAccount(space, id, draft) {
      requireUnlocked();
      const accounts = accountsBySpace[space] ?? [];
      const existing = accounts.find((account) => account.id === id);
      if (existing === undefined) throw new Error("The account was not found");
      if (!existing.canEdit) throw new Error("The account cannot be edited");
      const updated = { ...existing, label: draft.label, isDefault: draft.isDefault };
      accountsBySpace[space] = accounts.map((account) =>
        account.id === id ? updated : { ...account, isDefault: draft.isDefault ? false : account.isDefault },
      );
      return updated;
    },

    async deleteAccount(space, id) {
      requireUnlocked();
      const accounts = accountsBySpace[space] ?? [];
      const existing = accounts.find((account) => account.id === id);
      if (existing === undefined) throw new Error("The account was not found");
      if (!existing.canEdit) throw new Error("The account cannot be edited");
      accountsBySpace[space] = accounts.filter((account) => account.id !== id);
    },

    async createTaxonomy(space, kind, draft) {
      requireUnlocked();
      const values = taxonomyOf(space);
      const created: TaxonomyValue = {
        id: `fixture-${kind}-${values.filter((value) => value.kind === kind).length + 1}`,
        kind,
        label: draft.label,
        foregroundHex: kind === "tag" ? draft.foregroundHex ?? "#242424" : null,
        backgroundHex: kind === "tag" ? draft.backgroundHex ?? "#EDEDED" : null,
        canEdit: true,
        priority: kind === "category" ? draft.priority ?? "Useful" : undefined,
      };
      taxonomyBySpace[space] = [...values, created];
      return created;
    },

    async updateTaxonomy(space, kind, id, draft) {
      requireUnlocked();
      const values = taxonomyOf(space);
      const existing = values.find((value) => value.kind === kind && value.id === id);
      if (existing === undefined) throw new Error("The value was not found");
      if (existing.canEdit === false) throw new Error("The value cannot be edited");
      const updated: TaxonomyValue = {
        ...existing,
        label: draft.label,
        foregroundHex: kind === "tag" ? draft.foregroundHex ?? existing.foregroundHex : null,
        backgroundHex: kind === "tag" ? draft.backgroundHex ?? existing.backgroundHex : null,
        priority: kind === "category" ? draft.priority ?? existing.priority ?? "Useful" : undefined,
      };
      taxonomyBySpace[space] = values.map((value) => (value.id === id ? updated : value));
      return updated;
    },

    async deleteTaxonomy(space, kind, id) {
      requireUnlocked();
      const values = taxonomyOf(space);
      const existing = values.find((value) => value.kind === kind && value.id === id);
      if (existing === undefined) throw new Error("The value was not found");
      if (existing.canEdit === false) throw new Error("The value cannot be edited");
      taxonomyBySpace[space] = values.filter((value) => value.id !== id);
    },

    async resolveFilter(filter: TransactionFilter): Promise<FilterResolution> {
      requireUnlocked();

      const categoryIsKnown =
        filter.category === null || knowsTaxonomy(filter.space, "category", filter.category);
      const merchantIsKnown =
        filter.merchant === null || knowsTaxonomy(filter.space, "merchant", filter.merchant);
      const tagIsKnown = filter.tag === null || knowsTaxonomy(filter.space, "tag", filter.tag);
      const accountIsKnown = filter.account === null || knowsAccount(filter.space, filter.account);

      const removed: FilterFacet[] = [];
      if (!categoryIsKnown) removed.push("category");
      if (!merchantIsKnown) removed.push("merchant");
      if (!tagIsKnown) removed.push("tag");
      if (!accountIsKnown) removed.push("account");

      return {
        filter: {
          ...filter,
          category: categoryIsKnown ? filter.category : null,
          merchant: merchantIsKnown ? filter.merchant : null,
          tag: tagIsKnown ? filter.tag : null,
          account: accountIsKnown ? filter.account : null,
        },
        removed,
      };
    },

    async listTransactions(space: SpaceId): Promise<TransactionView[]> {
      requireUnlocked();
      return transactionsBySpace[space] ?? [];
    },

    async queryTransactions(
      filter: TransactionFilter,
      page: PageRequest,
    ): Promise<TransactionPage> {
      requireUnlocked();

      const matching = (transactionsBySpace[filter.space] ?? [])
        .filter((transaction) => matchesFilter(transaction, filter))
        .sort((left, right) => Date.parse(right.occurredAt) - Date.parse(left.occurredAt));

      return {
        rows: matching.slice(page.offset, page.offset + page.limit),
        totalRows: matching.length,
      };
    },

    async getTransaction(space: SpaceId, id: RecordId): Promise<TransactionView | null> {
      requireUnlocked();
      return (transactionsBySpace[space] ?? []).find((transaction) => transaction.id === id) ?? null;
    },

    async saveTransaction(draft: TransactionDraft): Promise<TransactionView> {
      requireUnlocked();

      validateTransactionCurrencies(
        draft,
        (accountsBySpace[draft.space] ?? []).map((account) => ({ id: account.id, currency: account.currency })),
      );

      savedTransactionCount += 1;
      const saved: TransactionView = {
        id: draft.id ?? `fixture-transaction-${savedTransactionCount}`,
        kind: draft.kind,
        amountMinorUnits: draft.amountMinorUnits,
        currency: draft.currency,
        occurredAt: draft.occurredAt,
        accountId: draft.accountId,
        counterpartyAccountId: draft.counterpartyAccountId ?? null,
        isCounterpartyPrivate: false,
        categoryId: draft.categoryId,
        merchantId:
          draft.merchantLabel === null
            ? null
            : findTaxonomyIdByLabel(draft.space, "merchant", draft.merchantLabel),
        tagIds: draft.tagLabels
          .map((label) => findTaxonomyIdByLabel(draft.space, "tag", label))
          .filter((tagId): tagId is RecordId => tagId !== null),
        reason: draft.reason,
        canEdit: true,
      };

      const rows = transactionsBySpace[draft.space] ?? [];
      const existingIndex = rows.findIndex((transaction) => transaction.id === saved.id);
      if (existingIndex === -1) rows.push(saved);
      else rows[existingIndex] = saved;
      transactionsBySpace[draft.space] = rows;

      return saved;
    },

    async deleteTransaction(space: SpaceId, id: RecordId): Promise<void> {
      requireUnlocked();
      const rows = transactionsBySpace[space] ?? [];
      const transaction = rows.find((item) => item.id === id);
      if (transaction === undefined) throw new Error("The transaction was not found");
      if (!transaction.canEdit) throw new Error("The transaction cannot be edited");
      transactionsBySpace[space] = rows.filter((item) => item.id !== id);
    },
  };
};
