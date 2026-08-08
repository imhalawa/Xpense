import type {
  AccountView,
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

const millisecondsPerDay = 24 * 60 * 60 * 1000;
const lockedMessage = "The vault is locked";

export interface FixtureSeed {
  spaces: SpaceSummary[];
  accounts: Record<SpaceId, AccountView[]>;
  taxonomy: Record<SpaceId, TaxonomyValue[]>;
  transactions: Record<SpaceId, TransactionView[]>;
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
  for (const [space, transactions] of Object.entries(seed.transactions)) {
    transactionsBySpace[space] = [...transactions];
  }

  let currentState: VaultState = "ready";
  let savedTransactionCount = 0;
  const listeners = new Set<(state: VaultState) => void>();

  const moveTo = (state: VaultState): void => {
    currentState = state;
    for (const listener of listeners) listener(state);
  };

  const requireUnlocked = (): void => {
    if (currentState === "locked") throw new Error(lockedMessage);
  };

  const taxonomyOf = (space: SpaceId): TaxonomyValue[] => seed.taxonomy[space] ?? [];

  const knowsTaxonomy = (space: SpaceId, kind: TaxonomyKind, id: RecordId): boolean =>
    taxonomyOf(space).some((value) => value.kind === kind && value.id === id);

  const knowsAccount = (space: SpaceId, id: RecordId): boolean =>
    (seed.accounts[space] ?? []).some((account) => account.id === id);

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
      return seed.accounts[space] ?? [];
    },

    async listTaxonomy(space: SpaceId, kind: TaxonomyKind): Promise<TaxonomyValue[]> {
      requireUnlocked();
      return taxonomyOf(space).filter((value) => value.kind === kind);
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

    async saveTransaction(draft: TransactionDraft): Promise<TransactionView> {
      requireUnlocked();

      savedTransactionCount += 1;
      const saved: TransactionView = {
        id: draft.id ?? `fixture-transaction-${savedTransactionCount}`,
        kind: draft.kind,
        amountMinorUnits: draft.amountMinorUnits,
        currency: draft.currency,
        occurredAt: draft.occurredAt,
        accountId: draft.accountId,
        counterpartyAccountId: null,
        isCounterpartyPrivate: false,
        categoryId: draft.categoryId,
        merchantId:
          draft.merchantLabel === null
            ? null
            : findTaxonomyIdByLabel(draft.space, "merchant", draft.merchantLabel),
        tagIds: draft.tagLabels
          .map((label) => findTaxonomyIdByLabel(draft.space, "tag", label))
          .filter((tagId): tagId is RecordId => tagId !== null),
        canEdit: true,
      };

      const rows = transactionsBySpace[draft.space] ?? [];
      const existingIndex = rows.findIndex((transaction) => transaction.id === saved.id);
      if (existingIndex === -1) rows.push(saved);
      else rows[existingIndex] = saved;
      transactionsBySpace[draft.space] = rows;

      return saved;
    },
  };
};
