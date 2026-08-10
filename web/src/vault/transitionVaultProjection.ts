import axios from "axios";
import type { SealedBytes } from "../crypto/primitives";
import type { EncryptedRecordResult } from "../crypto/worker/commands";
import type { VaultRecord } from "./vaultDatabase";
import type {
  AccountDraft,
  BudgetDraft,
  CategoryCreationPriority,
  PageRequest,
  RecordId,
  SpaceId,
  TaxonomyDraft,
  TaxonomyKind,
  TransactionDraft,
  TransactionFilter,
  VaultProjection,
  VaultState,
} from "./VaultProjection";

export type ProductionDataMode = "unknown" | "legacy" | "claiming" | "encrypted";

export interface ProjectionCryptoBridge {
  ownerId: string;
  decrypt(record: VaultRecord): Promise<Uint8Array>;
  encryptNew(record: VaultRecord, plaintext: Uint8Array): Promise<EncryptedRecordResult>;
  encryptReplacement(record: VaultRecord, plaintext: Uint8Array): Promise<SealedBytes>;
}

export interface EncryptedProjection extends VaultProjection {
  attachCrypto(bridge: ProjectionCryptoBridge): void;
}

interface TransitionOptions {
  legacy: VaultProjection;
  encrypted: EncryptedProjection;
  status?: () => Promise<Exclude<ProductionDataMode, "unknown">>;
}

export interface TransitionVaultProjection extends EncryptedProjection {
  readonly dataMode: ProductionDataMode;
}

const statusFromServer = async (): Promise<Exclude<ProductionDataMode, "unknown">> => {
  const mode = (await axios.get<{ mode: unknown }>("/api/v1/claim/status")).data.mode;
  if (mode !== "legacy" && mode !== "claiming" && mode !== "encrypted") {
    throw new Error("The server data mode is invalid.");
  }
  return mode;
};

export const transitionVaultProjection = ({
  legacy,
  encrypted,
  status = statusFromServer,
}: TransitionOptions): TransitionVaultProjection => {
  let currentState: VaultState = "locked";
  let mode: ProductionDataMode = "unknown";
  let selected: VaultProjection | null = null;
  const listeners = new Set<(state: VaultState) => void>();

  const moveTo = (state: VaultState): void => {
    currentState = state;
    for (const listener of listeners) listener(state);
  };

  const requireSelected = (): VaultProjection => {
    if (selected === null) throw new Error("The application data mode is unavailable.");
    return selected;
  };

  const choose = async (): Promise<void> => {
    if (mode !== "unknown") return;
    const resolved = await status();
    if (resolved !== "legacy" && resolved !== "claiming" && resolved !== "encrypted") {
      throw new Error("The server data mode is invalid.");
    }
    mode = resolved;
    selected = resolved === "legacy" ? legacy : resolved === "encrypted" ? encrypted : null;
  };

  return {
    get state() {
      return currentState;
    },
    get dataMode() {
      return mode;
    },
    subscribe(listener) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    attachCrypto(bridge) {
      encrypted.attachCrypto(bridge);
    },
    async unlock() {
      moveTo("loading");
      try {
        await choose();
        if (mode === "claiming") {
          moveTo("locked");
          return;
        }
        await requireSelected().unlock();
        moveTo("ready");
      } catch (error) {
        moveTo("error");
        throw error;
      }
    },
    lock() {
      encrypted.lock();
      if (selected === legacy) legacy.lock();
      moveTo("locked");
    },
    listSpaces: () => requireSelected().listSpaces(),
    listAccounts: (space: SpaceId) => requireSelected().listAccounts(space),
    listAccountBalances: (space: SpaceId) => requireSelected().listAccountBalances(space),
    listTaxonomy: (space: SpaceId, kind: TaxonomyKind) => requireSelected().listTaxonomy(space, kind),
    listBudgets: (space: SpaceId, on: Date) => requireSelected().listBudgets(space, on),
    saveBudget: (space: SpaceId, draft: BudgetDraft) => requireSelected().saveBudget(space, draft),
    deleteBudget: (space: SpaceId, id: RecordId) => requireSelected().deleteBudget(space, id),
    createCategory: (space: SpaceId, label: string, priority: CategoryCreationPriority) =>
      requireSelected().createCategory(space, label, priority),
    createAccount: (space: SpaceId, draft: AccountDraft) =>
      requireSelected().createAccount(space, draft),
    updateAccount: (space: SpaceId, id: RecordId, draft: AccountDraft) =>
      requireSelected().updateAccount(space, id, draft),
    deleteAccount: (space: SpaceId, id: RecordId) => requireSelected().deleteAccount(space, id),
    createTaxonomy: (space: SpaceId, kind: TaxonomyKind, draft: TaxonomyDraft) =>
      requireSelected().createTaxonomy(space, kind, draft),
    updateTaxonomy: (space: SpaceId, kind: TaxonomyKind, id: RecordId, draft: TaxonomyDraft) =>
      requireSelected().updateTaxonomy(space, kind, id, draft),
    deleteTaxonomy: (space: SpaceId, kind: TaxonomyKind, id: RecordId) =>
      requireSelected().deleteTaxonomy(space, kind, id),
    resolveFilter: (filter: TransactionFilter) => requireSelected().resolveFilter(filter),
    listTransactions: (space: SpaceId) => requireSelected().listTransactions(space),
    queryTransactions: (filter: TransactionFilter, page: PageRequest) =>
      requireSelected().queryTransactions(filter, page),
    getTransaction: (space: SpaceId, id: RecordId) => requireSelected().getTransaction(space, id),
    saveTransaction: (draft: TransactionDraft) => requireSelected().saveTransaction(draft),
    deleteTransaction: (space: SpaceId, id: RecordId) =>
      requireSelected().deleteTransaction(space, id),
  };
};
