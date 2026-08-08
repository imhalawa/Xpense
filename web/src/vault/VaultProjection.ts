import { Currency } from "../typings/enums/Currency";
import { TransactionKind } from "../clients/types";

export type SpaceId = string;
export type RecordId = string;
export type TaxonomyKind = "category" | "merchant" | "tag";
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
}

export interface TaxonomyValue {
  id: RecordId;
  kind: TaxonomyKind;
  label: string;
  foregroundHex: string | null;
  backgroundHex: string | null;
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
  categoryId: RecordId | null;
  merchantLabel: string | null;
  tagLabels: string[];
  reason: string | null;
}

export interface VaultProjection {
  readonly state: VaultState;
  subscribe(listener: (state: VaultState) => void): () => void;
  unlock(): Promise<void>;
  lock(): void;
  listSpaces(): Promise<SpaceSummary[]>;
  listAccounts(space: SpaceId): Promise<AccountView[]>;
  listTaxonomy(space: SpaceId, kind: TaxonomyKind): Promise<TaxonomyValue[]>;
  resolveFilter(filter: TransactionFilter): Promise<FilterResolution>;
  queryTransactions(filter: TransactionFilter, page: PageRequest): Promise<TransactionPage>;
  saveTransaction(draft: TransactionDraft): Promise<TransactionView>;
}
