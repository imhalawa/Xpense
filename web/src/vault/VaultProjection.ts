import { Currency } from "../typings/enums/Currency";
import {
  IBudgetPeriodResponse,
  IMoneyResponse,
  Recurrence,
  TransactionKind,
} from "../clients/types";

export type SpaceId = string;
export type RecordId = string;
export type TaxonomyKind = "category" | "merchant" | "tag";
export type CategoryPriority =
  | "Essential"
  | "Important"
  | "Useful"
  | "Optional"
  | "Avoidable";
export type CategoryCreationPriority = CategoryPriority | "Low" | "Medium" | "High";
export type VaultState = "locked" | "loading" | "ready" | "error";
export type FilterFacet = "space" | "category" | "merchant" | "tag" | "account" | "from" | "to";

export interface SpaceSummary {
  id: SpaceId;
  name: string;
  kind: "personal" | "group";
  canEdit: boolean;
}

export interface AccountView {
  id: RecordId;
  label: string;
  currency: Currency;
  canEdit: boolean;
  balanceSource?: "opening" | "current";
  openingBalanceMinorUnits?: number;
  balanceMinorUnits?: number;
  isDefault?: boolean;
}

export interface CurrencyBalanceView {
  currency: Currency;
  minorUnits: number;
}

export type AccountBalanceProjection =
  | { state: "available"; balances: CurrencyBalanceView[] }
  | { state: "unavailable"; reason: string };

export interface TaxonomyValue {
  id: RecordId;
  kind: TaxonomyKind;
  label: string;
  foregroundHex: string | null;
  backgroundHex: string | null;
  canEdit?: boolean;
  priority?: CategoryPriority;
}

export interface AccountDraft {
  label: string;
  currency: Currency;
  openingBalanceMinorUnits: number;
  isDefault: boolean;
}

export interface TaxonomyDraft {
  label: string;
  priority?: CategoryPriority;
  foregroundHex?: string;
  backgroundHex?: string;
}

export interface TransactionView {
  id: RecordId;
  kind: TransactionKind;
  amountMinorUnits: number;
  currency: Currency;
  occurredAt: string;
  accountId: RecordId | null;
  counterpartyAccountId: RecordId | null;
  isCounterpartyPrivate: boolean;
  categoryId: RecordId | null;
  merchantId: RecordId | null;
  tagIds: RecordId[];
  reason?: string | null;
  canEdit: boolean;
}

export interface TransactionFilter {
  space: SpaceId;
  category: RecordId | null;
  merchant: RecordId | null;
  tag: RecordId | null;
  account: RecordId | null;
  from: string | null;
  to: string | null;
}

export interface FilterResolution {
  filter: TransactionFilter;
  removed: FilterFacet[];
}

export interface PageRequest {
  offset: number;
  limit: number;
}

export interface TransactionPage {
  rows: TransactionView[];
  totalRows: number;
}

export interface TransactionDraft {
  id: RecordId | null;
  space: SpaceId;
  kind: TransactionKind;
  amountMinorUnits: number;
  currency: Currency;
  occurredAt: string;
  accountId: RecordId;
  counterpartyAccountId?: RecordId | null;
  categoryId: RecordId | null;
  merchantLabel: string | null;
  tagLabels: string[];
  reason: string | null;
}

export interface BudgetView {
  id: RecordId;
  category: {
    id: RecordId;
    label: string;
  };
  amount: IMoneyResponse;
  recurrence: Recurrence;
  startsOn: string;
  endsOn: string | null;
  alertThresholdPercent: number | null;
  period: IBudgetPeriodResponse | null;
  createdAt: string;
  updatedAt: string | null;
  canEdit: boolean;
}

export interface BudgetDraft {
  id: RecordId | null;
  categoryId: RecordId;
  amount: IMoneyResponse;
  recurrence: Recurrence;
  startsOn: string;
  endsOn: string | null;
  alertThresholdPercent: number | null;
}

export interface VaultProjection {
  readonly state: VaultState;
  readonly dataMode?: "unknown" | "legacy" | "claiming" | "encrypted";
  subscribe(listener: (state: VaultState) => void): () => void;
  unlock(): Promise<void>;
  lock(): void;
  listSpaces(): Promise<SpaceSummary[]>;
  listAccounts(space: SpaceId): Promise<AccountView[]>;
  listAccountBalances(space: SpaceId): Promise<AccountBalanceProjection>;
  listTaxonomy(space: SpaceId, kind: TaxonomyKind): Promise<TaxonomyValue[]>;
  listBudgets(space: SpaceId, on: Date): Promise<BudgetView[]>;
  saveBudget(space: SpaceId, draft: BudgetDraft): Promise<BudgetView>;
  deleteBudget(space: SpaceId, id: RecordId): Promise<void>;
  createCategory(
    space: SpaceId,
    label: string,
    priority: CategoryCreationPriority,
  ): Promise<TaxonomyValue>;
  createAccount(space: SpaceId, draft: AccountDraft): Promise<AccountView>;
  updateAccount(space: SpaceId, id: RecordId, draft: AccountDraft): Promise<AccountView>;
  deleteAccount(space: SpaceId, id: RecordId): Promise<void>;
  createTaxonomy(
    space: SpaceId,
    kind: TaxonomyKind,
    draft: TaxonomyDraft,
  ): Promise<TaxonomyValue>;
  updateTaxonomy(
    space: SpaceId,
    kind: TaxonomyKind,
    id: RecordId,
    draft: TaxonomyDraft,
  ): Promise<TaxonomyValue>;
  deleteTaxonomy(space: SpaceId, kind: TaxonomyKind, id: RecordId): Promise<void>;
  resolveFilter(filter: TransactionFilter): Promise<FilterResolution>;
  listTransactions(space: SpaceId): Promise<TransactionView[]>;
  queryTransactions(filter: TransactionFilter, page: PageRequest): Promise<TransactionPage>;
  getTransaction(space: SpaceId, id: RecordId): Promise<TransactionView | null>;
  saveTransaction(draft: TransactionDraft): Promise<TransactionView>;
  deleteTransaction(space: SpaceId, id: RecordId): Promise<void>;
}
