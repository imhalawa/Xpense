import { ConflictManager } from "./conflicts";
import {
  createSyncRecords,
  deleteSyncRecord,
  replaceSyncRecord,
  SyncConflictError,
  type CreateSyncRecord,
  type ReplaceSyncRecord,
  type SyncRecord,
} from "./syncClient";
import type {
  VaultDatabase,
  VaultOutboxEntry,
  VaultOutboxMutation,
  VaultRecord,
} from "../vault/vaultDatabase";

export interface OutboxRequest {
  kind: "create" | "replace" | "delete";
  recordId: string;
  expectedRevision?: number;
  payload?: Uint8Array;
}

export interface OutboxMutationBuilder {
  build(request: OutboxRequest): Promise<VaultOutboxMutation>;
}

export interface OptimisticProjection {
  apply(record: VaultRecord, payload: Uint8Array): void | Promise<void>;
  remove?(recordId: string): void | Promise<void>;
}

export interface SyncMutationApi {
  create(request: { records: CreateSyncRecord[] }, signal?: AbortSignal): Promise<SyncRecord[]>;
  replace(id: string, request: ReplaceSyncRecord, signal?: AbortSignal): Promise<SyncRecord>;
  remove(id: string, signal?: AbortSignal): Promise<void>;
}

const syncMutationApi: SyncMutationApi = {
  create: createSyncRecords,
  replace: replaceSyncRecord,
  remove: deleteSyncRecord,
};

const abortError = (): DOMException => new DOMException("The sync was cancelled", "AbortError");

const isAbortError = (error: unknown): boolean =>
  typeof error === "object" && error !== null && "name" in error && error.name === "AbortError";

const assertNotAborted = (signal: AbortSignal | undefined): void => {
  if (signal?.aborted) throw abortError();
};

const createRequest = (entry: VaultOutboxEntry): CreateSyncRecord => {
  if (entry.mutation.kind !== "create") throw new Error("The queued mutation is not a create");
  return { ...entry.mutation.request, idempotencyKey: entry.idempotencyKey };
};

export class OutboxManager {
  private replayPromise: Promise<void> | undefined;

  constructor(
    private readonly database: VaultDatabase,
    private readonly mutations: OutboxMutationBuilder,
    private readonly projection: OptimisticProjection,
    readonly conflicts: ConflictManager,
    readonly syncApi: SyncMutationApi = syncMutationApi,
  ) {}

  async queue(request: OutboxRequest, signal?: AbortSignal): Promise<VaultOutboxEntry> {
    return (await this.queueBatch([request], signal))[0]!;
  }

  async queueBatch(
    requests: readonly OutboxRequest[],
    signal?: AbortSignal,
  ): Promise<VaultOutboxEntry[]> {
    assertNotAborted(signal);
    const mutations = await Promise.all(requests.map((request) => this.mutations.build(request)));
    assertNotAborted(signal);
    for (const [index, mutation] of mutations.entries()) {
      const request = requests[index]!;
      if (mutation.kind !== request.kind || mutation.record.id !== request.recordId) {
        throw new Error("The queued mutation does not match its request");
      }
    }
    const entries = await this.database.enqueueOutboxBatch(mutations.map((mutation) => ({
      operationId: crypto.randomUUID(),
      idempotencyKey: crypto.randomUUID(),
      mutation,
    })));
    assertNotAborted(signal);
    for (const [index, mutation] of mutations.entries()) {
      const request = requests[index]!;
      if (request.payload !== undefined) await this.projection.apply(mutation.record, request.payload);
      else if (mutation.kind === "delete") await this.projection.remove?.(mutation.record.id);
    }
    return entries;
  }

  replay(signal?: AbortSignal): Promise<void> {
    if (this.replayPromise !== undefined) return this.replayPromise;
    this.replayPromise = this.replayInOrder(signal).finally(() => {
      this.replayPromise = undefined;
    });
    return this.replayPromise;
  }

  async keepMine(recordId: string, signal?: AbortSignal): Promise<void> {
    const conflict = this.conflicts.get(recordId);
    if (conflict === undefined) throw new Error("The conflict was not found");
    assertNotAborted(signal);
    const mutation = await this.mutations.build({
      kind: "replace",
      recordId,
      expectedRevision: conflict.latest.revision,
      payload: conflict.mine,
    });
    if (mutation.kind !== "replace" || mutation.record.id !== recordId) {
      throw new Error("The queued replacement does not match the conflict");
    }
    const entry: VaultOutboxEntry = {
      operationId: conflict.outboxEntry.operationId,
      idempotencyKey: conflict.outboxEntry.idempotencyKey,
      sequence: conflict.outboxEntry.sequence,
      mutation,
    };
    await this.database.replaceOutbox(entry);
    this.conflicts.remove(recordId);
    await this.replay(signal);
  }

  async keepTheirs(recordId: string): Promise<void> {
    const conflict = this.conflicts.get(recordId);
    if (conflict === undefined) throw new Error("The conflict was not found");
    await this.database.acknowledgeOutbox(conflict.outboxEntry.operationId, conflict.latest);
    await this.projection.apply(conflict.latest, conflict.theirs);
    this.conflicts.remove(recordId);
  }

  private async replayInOrder(signal: AbortSignal | undefined): Promise<void> {
    for (const entry of await this.database.outboxEntries()) {
      assertNotAborted(signal);
      try {
        await this.replayEntry(entry, signal);
      } catch (error) {
        if (signal?.aborted || isAbortError(error)) throw abortError();
        if (error instanceof SyncConflictError && entry.mutation.kind === "replace") {
          await this.recordConflict(entry, error.record, signal);
        }
        return;
      }
    }
  }

  private async replayEntry(entry: VaultOutboxEntry, signal: AbortSignal | undefined): Promise<void> {
    const { mutation } = entry;
    switch (mutation.kind) {
      case "create": {
        const [created] = await this.syncApi.create({ records: [createRequest(entry)] }, signal);
        if (created === undefined) throw new Error("The create acknowledgement is missing");
        assertNotAborted(signal);
        await this.database.acknowledgeOutbox(entry.operationId, created);
        return;
      }
      case "replace": {
        const replaced = await this.syncApi.replace(mutation.record.id, mutation.request, signal);
        assertNotAborted(signal);
        await this.database.acknowledgeOutbox(entry.operationId, replaced);
        return;
      }
      case "delete":
        await this.syncApi.remove(mutation.record.id, signal);
        assertNotAborted(signal);
        await this.database.acknowledgeOutbox(entry.operationId);
    }
  }

  private async recordConflict(
    entry: VaultOutboxEntry,
    latest: SyncRecord,
    signal: AbortSignal | undefined,
  ): Promise<void> {
    const local = entry.mutation.record;
    const stagedEntry = { ...entry, latestServerRecord: latest };
    await this.database.replaceOutbox(stagedEntry);
    assertNotAborted(signal);
    this.conflicts.add({
      recordId: local.id,
      outboxEntry: stagedEntry,
      local,
      latest,
      mine: local.payload,
      theirs: latest.payload,
    });
  }
}
