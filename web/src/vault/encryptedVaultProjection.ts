import axios from "axios";
import { PROTOCOL_VERSION, type RecordType } from "../crypto/protocol";
import { VaultWorkerUnavailableError } from "../crypto/worker/vaultWorkerClient";
import { SyncLifecycleCoordinator } from "../sync/lifecycle";
import {
  createSyncRecords,
  deleteSyncRecord,
  replaceSyncRecord,
  SyncClient,
} from "../sync/syncClient";
import type { OutboxCipher, OptimisticProjection, SyncMutationApi } from "../sync/outbox";
import { fixtureProjection } from "./fixtureProjection";
import { decodeVaultPayloadV1, encodeVaultPayloadV1, parseVaultPayloadV1, type VaultPayloadV1 } from "./payloadV1";
import type { EncryptedProjection, ProjectionCryptoBridge } from "./transitionVaultProjection";
import { openVaultDatabase, type VaultDatabase, type VaultOutboxMutation, type VaultRecord } from "./vaultDatabase";
import type {
  AccountDraft,
  BudgetDraft,
  CategoryCreationPriority,
  CategoryPriority,
  SpaceId,
  TaxonomyValue,
  TransactionDraft,
  VaultProjection,
  VaultState,
} from "./VaultProjection";

const personalSpace = "personal";
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
const priorityAliases: Record<CategoryCreationPriority, CategoryPriority> = {
  Essential: "Essential",
  Important: "Important",
  Useful: "Useful",
  Optional: "Optional",
  Avoidable: "Avoidable",
  High: "Important",
  Medium: "Useful",
  Low: "Optional",
};

export interface EncryptedVaultDependencies {
  openDatabase?: () => Promise<VaultDatabase>;
  pull?: (database: VaultDatabase, bridge: ProjectionCryptoBridge, signal: AbortSignal) => Promise<void>;
  syncApi?: SyncMutationApi;
  now?: () => Date;
  id?: () => string;
}

const freshAntiforgery = async (): Promise<string> =>
  (await axios.get<{ requestToken: string }>("/api/v1/auth/antiforgery")).data.requestToken;

export const encryptedSyncMutationApi: SyncMutationApi = {
  create: async (request, signal) => createSyncRecords(request, signal, await freshAntiforgery()),
  replace: async (id, request, signal) => replaceSyncRecord(id, request, signal, await freshAntiforgery()),
  remove: async (id, signal) => deleteSyncRecord(id, signal, await freshAntiforgery()),
};

const personalEnvelope = (record: VaultRecord) => {
  const envelope = record.envelopes.find((candidate) => candidate.groupId === null);
  if (envelope === undefined) throw new Error("The personal record envelope is unavailable.");
  return envelope;
};

const common = (id: string, createdAt: string, updatedAt: string | null) => ({
  schemaVersion: 1 as const,
  recordId: id,
  createdAt,
  updatedAt,
});

export const encryptedVaultProjection = (
  lifecycle: SyncLifecycleCoordinator,
  dependencies: EncryptedVaultDependencies = {},
): EncryptedProjection => {
  const openDatabase = dependencies.openDatabase ?? openVaultDatabase;
  const now = dependencies.now ?? (() => new Date());
  const id = dependencies.id ?? (() => crypto.randomUUID());
  let currentState: VaultState = "locked";
  let cryptoBridge: ProjectionCryptoBridge | null = null;
  let database: VaultDatabase | null = null;
  let abortController: AbortController | null = null;
  let delegate: VaultProjection | null = null;
  let outbox: ReturnType<SyncLifecycleCoordinator["createOutboxManager"]> | null = null;
  const payloads = new Map<string, { record: VaultRecord; payload: VaultPayloadV1 }>();
  const listeners = new Set<(state: VaultState) => void>();

  const moveTo = (state: VaultState): void => {
    currentState = state;
    for (const listener of listeners) listener(state);
  };

  const requireReady = (): VaultProjection => {
    if (currentState !== "ready" || delegate === null) throw new Error("The vault is locked");
    return delegate;
  };

  const rebuild = (): void => {
    const active = [...payloads.values()].filter(({ record }) => !record.tombstone);
    const scales = new Map(
      active.filter(({ record }) => record.recordType === "necessityScale")
        .map(({ record, payload }) => [record.id, payload]),
    );
    const taxonomy: TaxonomyValue[] = [];
    for (const { record, payload } of active) {
      if (record.recordType === "category") {
        const scale = scales.get(payload.necessityScaleId as string);
        const priority = scale?.label;
        if (priority !== "Essential" && priority !== "Important" && priority !== "Useful" && priority !== "Optional" && priority !== "Avoidable") continue;
        taxonomy.push({ id: record.id, kind: "category", label: payload.label as string, foregroundHex: null, backgroundHex: null, canEdit: true, priority });
      }
      if (record.recordType === "merchant") taxonomy.push({ id: record.id, kind: "merchant", label: payload.label as string, foregroundHex: null, backgroundHex: null, canEdit: true });
      if (record.recordType === "tag") taxonomy.push({ id: record.id, kind: "tag", label: payload.label as string, foregroundHex: payload.foregroundColorHex as string | null, backgroundHex: payload.backgroundColorHex as string | null, canEdit: true });
    }
    const accounts = active.filter(({ record }) => record.recordType === "account").map(({ record, payload }) => ({
      id: record.id,
      label: payload.label as string,
      currency: payload.currency as never,
      canEdit: true,
      balanceSource: "opening" as const,
      openingBalanceMinorUnits: payload.openingBalanceMinorUnits as number,
      isDefault: payload.isDefault as boolean,
    }));
    const transactions = active.filter(({ record }) => record.recordType === "transaction" || record.recordType === "transfer").map(({ record, payload }) => {
      const kind = payload.kind as "income" | "expense" | "transfer";
      return {
        id: record.id,
        kind,
        amountMinorUnits: (payload.amount as { minorUnits: number }).minorUnits,
        currency: (payload.amount as { currency: never }).currency,
        occurredAt: payload.occurredAt as string,
        accountId: kind === "income" ? payload.destinationAccountId as string : payload.sourceAccountId as string,
        counterpartyAccountId: kind === "transfer" ? payload.destinationAccountId as string : null,
        isCounterpartyPrivate: false,
        categoryId: payload.categoryRecordId as string | null,
        merchantId: payload.merchantRecordId as string | null,
        tagIds: payload.tagRecordIds as string[],
        reason: payload.reason as string | null,
        canEdit: true,
      };
    });
    const budgets = active.filter(({ record }) => record.recordType === "budget").flatMap(({ record, payload }) => {
      const category = taxonomy.find((value) => value.kind === "category" && value.id === payload.categoryRecordId);
      if (category === undefined) return [];
      return [{
        id: record.id,
        category: { id: category.id, label: category.label },
        amount: payload.amount as never,
        recurrence: payload.recurrence as never,
        startsOn: payload.startsOn as string,
        endsOn: payload.endsOn as string | null,
        alertThresholdPercent: payload.alertThresholdPercent as number | null,
        period: null,
        createdAt: payload.createdAt,
        updatedAt: payload.updatedAt,
        canEdit: true,
      }];
    });
    delegate = fixtureProjection({
      spaces: [{ id: personalSpace, name: "Personal", kind: "personal", canEdit: true }],
      accounts: { [personalSpace]: accounts },
      taxonomy: { [personalSpace]: taxonomy },
      transactions: { [personalSpace]: transactions },
      budgets: { [personalSpace]: budgets },
      transactionHistoryComplete: { [personalSpace]: true },
    });
  };

  const apply: OptimisticProjection = {
    apply(record, plaintext) {
      payloads.set(record.id, { record, payload: decodeVaultPayloadV1(record.recordType, record.id, plaintext) });
      rebuild();
    },
    remove(recordId) {
      payloads.delete(recordId);
      rebuild();
    },
  };

  const cipher: OutboxCipher = {
    async decrypt(record) {
      if (cryptoBridge === null) throw new Error("The vault encryption Worker is unavailable.");
      return cryptoBridge.decrypt(record);
    },
    async encrypt(request): Promise<VaultOutboxMutation> {
      if (cryptoBridge === null) throw new Error("The vault encryption Worker is unavailable.");
      const existing = database === null ? undefined : await database.getRecord(request.recordId);
      if (request.kind === "delete") {
        if (existing === undefined) throw new Error("The encrypted record was not found.");
        return { kind: "delete", record: { ...existing, tombstone: true } };
      }
      if (request.plaintext === undefined) throw new Error("The encrypted payload is missing.");
      if (request.kind === "replace") {
        if (existing === undefined || request.expectedRevision !== existing.revision) throw new Error("The encrypted record revision changed.");
        const sealedPayload = await cryptoBridge.encryptReplacement(existing, request.plaintext);
        const record = { ...existing, revision: existing.revision + 1, nonce: sealedPayload.nonce, ciphertext: sealedPayload.ciphertext, serverUpdatedAt: now().toISOString() };
        return { kind: "replace", record, request: { expectedRevision: existing.revision, protocolVersion: PROTOCOL_VERSION, nonce: record.nonce, ciphertext: record.ciphertext } };
      }
      const pending = payloads.get(request.recordId);
      if (pending === undefined) throw new Error("The encrypted record metadata is missing.");
      const encrypted = await cryptoBridge.encryptNew(pending.record, request.plaintext);
      const record = { ...pending.record, nonce: encrypted.sealedPayload.nonce, ciphertext: encrypted.sealedPayload.ciphertext, envelopes: [{ id: pending.record.id, groupId: null, wrappedKey: encrypted.personalEnvelope.ciphertext, nonce: encrypted.personalEnvelope.nonce, encapsulatedKey: null, protocolVersion: PROTOCOL_VERSION }] };
      return { kind: "create", record, request: { id: record.id, recordType: recordTypeCodes[record.recordType], parentResourceId: record.parentResourceId, protocolVersion: PROTOCOL_VERSION, nonce: record.nonce, ciphertext: record.ciphertext, personalEnvelope: { wrappedKey: record.envelopes[0]!.wrappedKey, nonce: record.envelopes[0]!.nonce, protocolVersion: PROTOCOL_VERSION } } };
    },
  };

  const stage = async (recordType: RecordType, parentResourceId: string | null, payload: VaultPayloadV1, existing?: VaultRecord): Promise<void> => {
    if (outbox === null || cryptoBridge === null) throw new Error("The vault is locked");
    parseVaultPayloadV1(recordType, payload.recordId, payload);
    const bytes = encodeVaultPayloadV1(payload);
    let createMetadataStaged = false;
    try {
      if (existing === undefined) {
        const instant = payload.createdAt;
        payloads.set(payload.recordId, { payload, record: { id: payload.recordId, recordType, ownerId: cryptoBridge.ownerId, parentResourceId, revision: 1, protocolVersion: PROTOCOL_VERSION, nonce: new Uint8Array(), ciphertext: new Uint8Array(), envelopes: [], tombstone: false, sequenceNumber: 0, serverCreatedAt: instant, serverUpdatedAt: instant } });
        createMetadataStaged = true;
        await outbox.queue({ kind: "create", recordId: payload.recordId, plaintext: bytes }, abortController?.signal);
      } else {
        await outbox.queue({ kind: "replace", recordId: payload.recordId, expectedRevision: existing.revision, plaintext: bytes }, abortController?.signal);
      }
      await outbox.replay(abortController?.signal);
    } catch (error) {
      if (createMetadataStaged && database !== null && await database.outboxEntryForRecord(payload.recordId) === undefined) {
        payloads.delete(payload.recordId);
        rebuild();
      }
      throw error;
    } finally {
      bytes.fill(0);
    }
  };

  const activeAccounts = () =>
    [...payloads.values()].filter(({ record }) => record.recordType === "account" && !record.tombstone);

  const hasDefaultAccount = (): boolean =>
    activeAccounts().some(({ payload }) => payload.isDefault === true);

  const demoteOtherDefaultAccounts = async (keptRecordId: string): Promise<void> => {
    const demoted = activeAccounts().filter(
      ({ record, payload }) => record.id !== keptRecordId && payload.isDefault === true,
    );
    for (const { record, payload } of demoted) {
      await stage(
        "account",
        record.parentResourceId,
        { ...payload, isDefault: false, updatedAt: now().toISOString() },
        record,
      );
    }
  };

  const queueRemove = async (recordId: string): Promise<void> => {
    if (outbox === null) throw new Error("The vault is locked");
    await outbox.queue({ kind: "delete", recordId }, abortController?.signal);
  };

  const remove = async (recordId: string): Promise<void> => {
    await queueRemove(recordId);
    if (outbox === null) throw new Error("The vault is locked");
    await outbox.replay(abortController?.signal);
  };

  const api: EncryptedProjection = {
    get state() { return currentState; },
    attachCrypto(bridge) { cryptoBridge = bridge; },
    subscribe(listener) { listeners.add(listener); return () => listeners.delete(listener); },
    async unlock() {
      if (cryptoBridge === null) throw new Error("Unlock with a vault wrapper first.");
      moveTo("loading");
      const unlockController = new AbortController();
      abortController = unlockController;
      try {
        database = await openDatabase();
        const pull = dependencies.pull ?? (async (store, bridge, signal) => new SyncClient(store, bridge).pull(signal));
        await pull(database, cryptoBridge, unlockController.signal);
        payloads.clear();
        const quarantine = await database.quarantineEntries();
        const quarantined = new Set(quarantine.map((entry) => `${entry.recordId}:${entry.revision}`));
        const legacyQuarantined = new Set(
          quarantine.filter((entry) => entry.revision === 0).map((entry) => entry.recordId),
        );
        for (const record of await database.records()) {
          if (record.tombstone || record.ownerId !== cryptoBridge.ownerId) continue;
          if (legacyQuarantined.has(record.id) || quarantined.has(`${record.id}:${record.revision}`)) continue;
          let bytes: Uint8Array;
          try {
            personalEnvelope(record);
            bytes = await cryptoBridge.decrypt(record);
          } catch (error) {
            if (
              unlockController.signal.aborted ||
              error instanceof VaultWorkerUnavailableError
            ) {
              throw error;
            }
            await database.putQuarantine({ recordId: record.id, revision: record.revision, record, reason: "authentication-failed" });
            continue;
          }
          try {
            payloads.set(record.id, { record, payload: decodeVaultPayloadV1(record.recordType, record.id, bytes) });
          } catch {
            await database.putQuarantine({ recordId: record.id, revision: record.revision, record, reason: "invalid-payload" });
          }
          finally { bytes.fill(0); }
        }
        rebuild();
        outbox = lifecycle.createOutboxManager(database, cipher, apply, dependencies.syncApi ?? encryptedSyncMutationApi);
        await outbox.replay(unlockController.signal);
        moveTo("ready");
      } catch (error) {
        database?.close(); database = null; payloads.clear(); delegate = null; outbox = null;
        if (abortController === unlockController && !unlockController.signal.aborted) moveTo("error");
        throw error;
      }
    },
    lock() { abortController?.abort(); abortController = null; database?.close(); database = null; payloads.clear(); delegate = null; outbox = null; cryptoBridge = null; lifecycle.lock(); moveTo("locked"); },
    listSpaces: () => requireReady().listSpaces(),
    listAccounts: (space) => requireReady().listAccounts(space),
    listAccountBalances: (space) => requireReady().listAccountBalances(space),
    listTaxonomy: (space, kind) => requireReady().listTaxonomy(space, kind),
    listBudgets: (space, on) => requireReady().listBudgets(space, on),
    resolveFilter: (filter) => requireReady().resolveFilter(filter),
    listTransactions: (space) => requireReady().listTransactions(space),
    queryTransactions: (filter, page) => requireReady().queryTransactions(filter, page),
    getTransaction: (space, recordId) => requireReady().getTransaction(space, recordId),
    async createAccount(space: SpaceId, draft: AccountDraft) {
      if (space !== personalSpace) throw new Error("Group spaces are not available yet.");
      const recordId = id(); const instant = now().toISOString();
      const isDefault = draft.isDefault || !hasDefaultAccount();
      await stage("account", recordId, { ...common(recordId, instant, null), label: draft.label, balance: { minorUnits: draft.openingBalanceMinorUnits, currency: draft.currency }, openingBalanceMinorUnits: draft.openingBalanceMinorUnits, currency: draft.currency, isDefault });
      if (isDefault) await demoteOtherDefaultAccounts(recordId);
      return (await requireReady().listAccounts(space)).find((account) => account.id === recordId)!;
    },
    async updateAccount(space, recordId, draft) {
      const item = payloads.get(recordId); if (item?.record.recordType !== "account") throw new Error("The account was not found");
      if (item.payload.currency !== draft.currency) throw new Error("The account currency cannot be changed.");
      await stage("account", recordId, { ...item.payload, label: draft.label, openingBalanceMinorUnits: draft.openingBalanceMinorUnits, isDefault: draft.isDefault, updatedAt: now().toISOString() }, item.record);
      if (draft.isDefault) await demoteOtherDefaultAccounts(recordId);
      return (await requireReady().listAccounts(space)).find((account) => account.id === recordId)!;
    },
    deleteAccount: (_space, recordId) => remove(recordId),
    async createTaxonomy(space, kind, draft) {
      if (space !== personalSpace) throw new Error("Group spaces are not available yet.");
      const recordId = id(); const instant = now().toISOString();
      if (kind === "category") {
        const priority = draft.priority ?? "Useful";
        const scale = [...payloads.values()].find(({ record, payload }) => record.recordType === "necessityScale" && payload.label === priority);
        if (scale === undefined) throw new Error("The category priority is unavailable.");
        await stage("category", null, { ...common(recordId, instant, null), label: draft.label, necessityScaleId: scale.record.id, priority: { label: priority } });
      } else if (kind === "merchant") await stage("merchant", null, { ...common(recordId, instant, null), label: draft.label });
      else await stage("tag", null, { ...common(recordId, instant, null), label: draft.label, foregroundColorHex: draft.foregroundHex ?? null, backgroundColorHex: draft.backgroundHex ?? null });
      return (await requireReady().listTaxonomy(space, kind)).find((value) => value.id === recordId)!;
    },
    async createCategory(space, label, priority) { return api.createTaxonomy(space, "category", { label, priority: priorityAliases[priority] }); },
    async updateTaxonomy(space, kind, recordId, draft) {
      const item = payloads.get(recordId); if (item?.record.recordType !== kind) throw new Error(`The ${kind} was not found`);
      const changed: VaultPayloadV1 = { ...item.payload, label: draft.label, updatedAt: now().toISOString() };
      if (kind === "tag") { changed.foregroundColorHex = draft.foregroundHex ?? null; changed.backgroundColorHex = draft.backgroundHex ?? null; }
      if (kind === "category" && draft.priority !== undefined) {
        const scale = [...payloads.values()].find(({ record, payload }) => record.recordType === "necessityScale" && payload.label === draft.priority);
        if (scale === undefined) throw new Error("The category priority is unavailable.");
        changed.necessityScaleId = scale.record.id;
        changed.priority = { label: draft.priority };
      }
      await stage(kind, item.record.parentResourceId, changed, item.record);
      return (await requireReady().listTaxonomy(space, kind)).find((value) => value.id === recordId)!;
    },
    async deleteTaxonomy(_space, kind, recordId) {
      const cascade = kind === "category"
        ? [...payloads.values()]
            .filter(({ record, payload }) => record.recordType === "budget" && payload.categoryRecordId === recordId)
            .map(({ record }) => record.id)
        : [];
      if (outbox === null) throw new Error("The vault is locked");
      await outbox.queueBatch(
        [recordId, ...cascade].map((id) => ({ kind: "delete" as const, recordId: id })),
        abortController?.signal,
      );
      if (outbox === null) throw new Error("The vault is locked");
      await outbox.replay(abortController?.signal);
    },
    async saveBudget(space, draft: BudgetDraft) {
      const recordId = draft.id ?? id(); const existing = draft.id === null ? undefined : payloads.get(recordId)?.record; const instant = now().toISOString();
      if (draft.id !== null && (existing === undefined || existing.recordType !== "budget")) throw new Error("The budget was not found.");
      await stage("budget", recordId, { ...common(recordId, existing?.serverCreatedAt ?? instant, existing === undefined ? null : instant), categoryRecordId: draft.categoryId, amount: draft.amount, recurrence: draft.recurrence, startsOn: draft.startsOn, endsOn: draft.endsOn, alertThresholdPercent: draft.alertThresholdPercent }, existing);
      return (await requireReady().listBudgets(space, now())).find((budget) => budget.id === recordId)!;
    },
    deleteBudget: (_space, recordId) => remove(recordId),
    async saveTransaction(draft: TransactionDraft) {
      const recordId = draft.id ?? id(); const existingItem = draft.id === null ? undefined : payloads.get(recordId); const recordType = draft.kind === "transfer" ? "transfer" : "transaction";
      if (draft.id !== null && existingItem === undefined) throw new Error("The transaction was not found.");
      if (existingItem !== undefined && existingItem.record.recordType !== recordType) throw new Error("A transfer cannot change transaction type.");
      const instant = now().toISOString();
      const sourceAccountId = draft.kind === "income" ? null : draft.accountId;
      const destinationAccountId = draft.kind === "income" ? draft.accountId : draft.kind === "transfer" ? draft.counterpartyAccountId ?? null : null;
      if (draft.kind === "transfer" && (destinationAccountId === null || destinationAccountId === sourceAccountId)) {
        throw new Error("A transfer requires two different accounts.");
      }
      const parentResourceId = sourceAccountId ?? destinationAccountId;
      if (existingItem !== undefined && existingItem.record.parentResourceId !== parentResourceId) {
        throw new Error("The transaction account cannot be changed after creation.");
      }
      let merchantId: string | null = null;
      if (draft.merchantLabel !== null) {
        merchantId = (await api.listTaxonomy(draft.space, "merchant")).find((merchant) => merchant.label === draft.merchantLabel)?.id ?? (await api.createTaxonomy(draft.space, "merchant", { label: draft.merchantLabel })).id;
      }
      const tagIds: string[] = [];
      for (const label of draft.tagLabels) tagIds.push((await api.listTaxonomy(draft.space, "tag")).find((tag) => tag.label === label)?.id ?? (await api.createTaxonomy(draft.space, "tag", { label })).id);
      await stage(recordType, parentResourceId, { ...common(recordId, existingItem?.payload.createdAt ?? instant, existingItem === undefined ? null : instant), kind: draft.kind, amount: { minorUnits: draft.amountMinorUnits, currency: draft.currency }, sourceAccountId, destinationAccountId, categoryRecordId: draft.categoryId, merchantRecordId: merchantId, tagRecordIds: tagIds, reason: draft.reason, occurredAt: draft.occurredAt }, existingItem?.record);
      return (await requireReady().listTransactions(draft.space)).find((transaction) => transaction.id === recordId)!;
    },
    deleteTransaction: (_space, recordId) => remove(recordId),
  };
  return api;
};
