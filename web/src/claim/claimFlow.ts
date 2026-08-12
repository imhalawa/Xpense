import type { EncryptedRecordResult } from "../crypto/worker/commands";
import type { PayloadDescriptor, EnvelopeDescriptor } from "../crypto/protocol";
import type { CreateSyncRecord } from "../sync/syncClient";
import type {
  VaultDatabase,
  VaultOutboxEntry,
  VaultRecord,
} from "../vault/vaultDatabase";
import {
  parseClaimDataset,
  prepareClaimRecords,
  type LegacyClaimDataset,
  type PreparedClaimRecord,
} from "./claimDataset";

const encoder = new TextEncoder();
const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;
const claimCreatePrefix = "legacy-claim-create:";
const claimDeletePrefix = "legacy-claim-delete:";
const claimIdempotencyPrefix = "legacy-claim-v1:";

export type ClaimPhase =
  | "starting"
  | "downloading"
  | "validating"
  | "encrypting"
  | "uploading"
  | "verifying"
  | "complete";

export interface ClaimStatus {
  phase: ClaimPhase;
  completedRecords: number;
  totalRecords: number;
}

export interface ClaimCompletion {
  recordCount: number;
  counts: Record<string, number>;
  manifestHash: string;
  completedAt: string;
}

export interface ClaimApi {
  currentUserId(signal?: AbortSignal): Promise<string>;
  start(signal?: AbortSignal): Promise<{ claimToken: string; expiresAt: string }>;
  download(claimToken: string, signal?: AbortSignal): Promise<unknown>;
  upload(record: CreateSyncRecord, signal?: AbortSignal): Promise<VaultRecord>;
  remove(id: string, signal?: AbortSignal): Promise<void>;
  complete(claimToken: string, signal?: AbortSignal): Promise<ClaimCompletion>;
}

export interface ClaimRecordCipher {
  isReady(): boolean;
  encrypt(
    payload: Uint8Array,
    payloadDescriptor: PayloadDescriptor,
    personalEnvelopeDescriptor: EnvelopeDescriptor,
  ): Promise<EncryptedRecordResult>;
}

export interface RunLegacyClaimOptions {
  api: ClaimApi;
  cipher: ClaimRecordCipher;
  database: VaultDatabase;
  signal?: AbortSignal;
  onStatus?: (status: ClaimStatus) => void;
}

const abortError = (): DOMException => new DOMException("The legacy claim was cancelled.", "AbortError");

const assertNotAborted = (signal: AbortSignal | undefined): void => {
  if (signal?.aborted) throw abortError();
};

const status = (
  options: RunLegacyClaimOptions,
  phase: ClaimPhase,
  completedRecords: number,
  totalRecords: number,
): void => options.onStatus?.({ phase, completedRecords, totalRecords });

const orderRecords = (records: PreparedClaimRecord[]): PreparedClaimRecord[] =>
  [...records].sort((left, right) => {
    const leftRank = left.parentResourceId === left.id ? 0 : left.parentResourceId === null ? 1 : 2;
    const rightRank = right.parentResourceId === right.id ? 0 : right.parentResourceId === null ? 1 : 2;
    if (leftRank !== rightRank) return leftRank - rightRank;
    return left.id < right.id ? -1 : left.id > right.id ? 1 : 0;
  });

const createRequest = (entry: VaultOutboxEntry): CreateSyncRecord => {
  if (entry.mutation.kind !== "create") throw new Error("The staged legacy claim record is invalid.");
  return { ...entry.mutation.request, idempotencyKey: entry.idempotencyKey };
};

const clearObject = (value: unknown): void => {
  if (Array.isArray(value)) {
    for (const entry of value) clearObject(entry);
    value.splice(0);
    return;
  }
  if (value === null || typeof value !== "object") return;
  for (const [key, entry] of Object.entries(value)) {
    clearObject(entry);
    delete (value as Record<string, unknown>)[key];
  }
};

const clearDataset = (dataset: LegacyClaimDataset | null): void => {
  if (dataset === null) return;
  clearObject(dataset);
};

const localRecord = (
  record: PreparedClaimRecord,
  ownerId: string,
  payload: Uint8Array,
): VaultRecord => ({
  id: record.id,
  recordType: record.recordType,
  ownerId,
  parentResourceId: record.parentResourceId,
  revision: 1,
  payload,
  tombstone: false,
  sequenceNumber: 0,
  serverCreatedAt: record.createdAt,
  serverUpdatedAt: record.updatedAt ?? record.createdAt,
});

const stageCreate = async (
  options: RunLegacyClaimOptions,
  record: PreparedClaimRecord,
  ownerId: string,
): Promise<VaultOutboxEntry> => {
  const payload = encoder.encode(JSON.stringify(record.payload));
  assertNotAborted(options.signal);
  const stagedRecord = localRecord(record, ownerId, payload);
  return options.database.enqueueOutboxOnce({
    operationId: `${claimCreatePrefix}${record.id}`,
    idempotencyKey: `${claimIdempotencyPrefix}${record.id}`,
    mutation: {
      kind: "create",
      record: stagedRecord,
      request: {
        id: stagedRecord.id,
        recordType: record.recordTypeCode,
        parentResourceId: stagedRecord.parentResourceId,
        payload: stagedRecord.payload,
      },
    },
  });
};

const uploadCreate = async (
  options: RunLegacyClaimOptions,
  entry: VaultOutboxEntry,
): Promise<void> => {
  const created = await options.api.upload(createRequest(entry), options.signal);
  assertNotAborted(options.signal);
  await options.database.acknowledgeOutbox(entry.operationId, created);
};

const ensureCreated = async (
  options: RunLegacyClaimOptions,
  record: PreparedClaimRecord,
  ownerId: string,
): Promise<void> => {
  let pending = await options.database.outboxEntryForRecord(record.id);
  const stored = await options.database.getRecord(record.id);
  assertNotAborted(options.signal);
  if (pending?.mutation.kind === "delete" || (pending === undefined && stored !== undefined)) return;
  if (pending === undefined) pending = await stageCreate(options, record, ownerId);
  await uploadCreate(options, pending);
};

const ensureTombstone = async (
  options: RunLegacyClaimOptions,
  record: PreparedClaimRecord,
): Promise<void> => {
  let pending = await options.database.outboxEntryForRecord(record.id);
  let stored = await options.database.getRecord(record.id);
  assertNotAborted(options.signal);
  if (pending?.mutation.kind === "create") {
    await uploadCreate(options, pending);
    pending = undefined;
    stored = await options.database.getRecord(record.id);
  }
  if (pending === undefined && stored?.tombstone === true) return;
  if (pending === undefined) {
    if (stored === undefined) throw new Error("The legacy claim record was not staged.");
    pending = await options.database.enqueueOutboxOnce({
      operationId: `${claimDeletePrefix}${record.id}`,
      idempotencyKey: `${claimIdempotencyPrefix}${record.id}:delete`,
      mutation: { kind: "delete", record: { ...stored, tombstone: true } },
    });
  }
  if (pending.mutation.kind !== "delete") throw new Error("The staged legacy tombstone is invalid.");
  await options.api.remove(record.id, options.signal);
  assertNotAborted(options.signal);
  await options.database.acknowledgeOutbox(pending.operationId);
};

export const runLegacyClaim = async (
  options: RunLegacyClaimOptions,
): Promise<ClaimCompletion> => {
  if (!options.cipher.isReady()) {
    throw new Error("Unlock the vault before starting the legacy claim.");
  }
  let claimToken: string | null = null;
  let dataset: LegacyClaimDataset | null = null;
  let prepared: PreparedClaimRecord[] = [];
  try {
    assertNotAborted(options.signal);
    status(options, "starting", 0, 0);
    const [identity, started] = await Promise.all([
      options.api.currentUserId(options.signal),
      options.api.start(options.signal),
    ]);
    if (!uuidPattern.test(identity)) throw new Error("The authenticated user id is invalid.");
    claimToken = started.claimToken;
    assertNotAborted(options.signal);
    status(options, "downloading", 0, 0);
    const downloaded = await options.api.download(claimToken, options.signal);
    assertNotAborted(options.signal);
    status(options, "validating", 0, 0);
    dataset = await parseClaimDataset(downloaded);
    prepared = orderRecords(prepareClaimRecords(dataset));
    const totalRecords = prepared.length;
    let completedRecords = 0;
    for (const record of prepared) {
      assertNotAborted(options.signal);
      status(options, "encrypting", completedRecords, totalRecords);
      await ensureCreated(options, record, identity);
      completedRecords += 1;
      clearObject(record.payload);
      status(options, "uploading", completedRecords, totalRecords);
    }
    for (const record of prepared.filter((record) => record.tombstone)) {
      assertNotAborted(options.signal);
      await ensureTombstone(options, record);
    }
    assertNotAborted(options.signal);
    status(options, "verifying", totalRecords, totalRecords);
    const completion = await options.api.complete(claimToken, options.signal);
    if (completion.recordCount !== dataset.recordCount ||
        completion.manifestHash.toLowerCase() !== dataset.manifestHash ||
        Object.keys(completion.counts).length !== Object.keys(dataset.counts).length ||
        Object.entries(dataset.counts).some(([key, count]) => completion.counts[key] !== count)) {
      throw new Error("The server did not verify the expected legacy claim manifest.");
    }
    status(options, "complete", totalRecords, totalRecords);
    return completion;
  } finally {
    claimToken = null;
    for (const record of prepared) clearObject(record.payload);
    prepared.splice(0);
    clearDataset(dataset);
    dataset = null;
  }
};
