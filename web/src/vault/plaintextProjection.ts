import { listAccounts, listCategories, listMerchants, listTags } from "../clients/options";
import { createTransaction, listTransactions } from "../clients/transactions";
import { fixtureProjection } from "./fixtureProjection";
import type { FixtureSeed } from "./fixtureProjection";
import type {
  IAccountResponse,
  ICategoryResponse,
  ICreateTransactionRequest,
  IMerchantResponse,
  IOptionRequest,
  ITagResponse,
  ITransactionResponse,
} from "../clients/types";
import type {
  AccountView,
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
const editingUnsupportedMessage = "Editing a transaction is not supported yet";
const transferUnsupportedMessage = "Saving a transfer is not supported yet";

const personalSpace: SpaceSummary = {
  id: personalSpaceId,
  name: "Personal",
  kind: "personal",
  canEdit: true,
};

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
});

const toCategoryValue = (category: ICategoryResponse): TaxonomyValue => ({
  id: String(category.id),
  kind: "category",
  label: category.label,
  foregroundHex: null,
  backgroundHex: null,
});

const toMerchantValue = (merchant: IMerchantResponse): TaxonomyValue => ({
  id: String(merchant.id),
  kind: "merchant",
  label: merchant.label,
  foregroundHex: null,
  backgroundHex: null,
});

const toTagValue = (tag: ITagResponse): TaxonomyValue => ({
  id: String(tag.id),
  kind: "tag",
  label: tag.label,
  foregroundHex: tag.fgColorHex,
  backgroundHex: tag.bgColorHex,
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
    const [accounts, categories, merchants, tags] = await Promise.all([
      listAccounts(),
      listCategories(),
      listMerchants(),
      listTags(),
    ]);
    const ledger = await loadLedger();

    taxonomy = [
      ...categories.map(toCategoryValue),
      ...merchants.map(toMerchantValue),
      ...tags.map(toTagValue),
    ];

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
    destinationAccountNumber: draft.kind === "income" ? draft.accountId : null,
    categoryId: draft.categoryId === null ? null : Number(draft.categoryId),
    merchant: draft.merchantLabel === null ? null : optionRequest("merchant", draft.merchantLabel),
    tags: draft.tagLabels.map((label) => optionRequest("tag", label)),
    reason: draft.reason,
    occurredAt: draft.occurredAt,
  });

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

    async resolveFilter(filter) {
      return requireLoaded().resolveFilter(filter);
    },

    async queryTransactions(filter, page) {
      return requireLoaded().queryTransactions(filter, page);
    },

    async saveTransaction(draft) {
      requireLoaded();
      if (draft.id !== null) throw new Error(editingUnsupportedMessage);
      if (draft.kind === "transfer") throw new Error(transferUnsupportedMessage);

      const created = await createTransaction(toCreateRequest(draft));
      await load();
      return toTransactionView(created);
    },
  };
};
