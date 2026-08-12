import axios from "axios";
import { decodeVaultPayloadV1 } from "../vault/payloadV1";
import {
  recordTypes,
  type RecordType,
  type VaultDatabase,
  type VaultRecord,
  type VaultSyncMutation,
} from "../vault/vaultDatabase";

export interface SyncRecord {
  id: string;
  recordType: RecordType;
  ownerId: string;
  parentResourceId: string | null;
  revision: number;
  payload: Uint8Array;
  tombstone: boolean;
  sequenceNumber: number;
  serverCreatedAt: string;
  serverUpdatedAt: string;
}

type ByteValue = Uint8Array | number[] | string;

interface WireSyncRecord {
  id: string;
  recordType: number;
  ownerId: string;
  parentResourceId: string | null;
  revision: number;
  payload: ByteValue;
  tombstone: boolean;
  sequenceNumber: number;
  serverCreatedAt: string;
  serverUpdatedAt: string;
}

interface WireSyncChanges {
  records: WireSyncRecord[];
  nextCursor: string;
  hasMore: boolean;
}

export interface SyncChanges {
  records: SyncRecord[];
  nextCursor: string;
  hasMore: boolean;
}

export class SyncConflictError extends Error {
  constructor(readonly record: SyncRecord) {
    super("The record was changed on another device");
  }
}

export interface CreateSyncRecord {
  id: string;
  idempotencyKey: string;
  recordType: number;
  parentResourceId: string | null;
  payload: Uint8Array;
}

export interface ReplaceSyncRecord {
  expectedRevision: number;
  payload: Uint8Array;
}

interface WireCreateSyncRecord extends Omit<CreateSyncRecord, "payload"> {
  payload: string;
}

interface WireReplaceSyncRecord extends Omit<ReplaceSyncRecord, "payload"> {
  payload: string;
}

const byteValue = (value: ByteValue): Uint8Array => {
  if (value instanceof Uint8Array) return new Uint8Array(value);
  if (Array.isArray(value)) return new Uint8Array(value);
  const decoded = atob(value);
  return Uint8Array.from(decoded, (character) => character.charCodeAt(0));
};

const base64 = (value: Uint8Array): string => {
  let binary = "";
  const chunkSize = 32_768;
  for (let start = 0; start < value.length; start += chunkSize) {
    binary += String.fromCharCode(...value.subarray(start, start + chunkSize));
  }
  return btoa(binary);
};

const toWireCreateRecord = (record: CreateSyncRecord): WireCreateSyncRecord => ({
  id: record.id,
  idempotencyKey: record.idempotencyKey,
  recordType: record.recordType,
  parentResourceId: record.parentResourceId,
  payload: base64(record.payload),
});

const toWireReplaceRecord = (record: ReplaceSyncRecord): WireReplaceSyncRecord => ({
  expectedRevision: record.expectedRevision,
  payload: base64(record.payload),
});

const toSyncRecord = (record: WireSyncRecord): SyncRecord => {
  const recordType = recordTypes[record.recordType];
  if (recordType === undefined) throw new Error("The sync record type is not supported");
  return { ...record, recordType, payload: byteValue(record.payload) };
};

const assertNotAborted = (signal: AbortSignal | undefined): void => {
  if (signal?.aborted) throw new Error("The sync was cancelled");
};

const mutationConfig = (
  signal: AbortSignal | undefined,
  antiforgeryToken: string | undefined,
): { signal?: AbortSignal; headers?: Record<string, string> } => ({
  ...(signal === undefined ? {} : { signal }),
  ...(antiforgeryToken === undefined
    ? {}
    : { headers: { "X-Xpense-Antiforgery": antiforgeryToken } }),
});

export const getSyncChanges = async (
  cursor?: string | null,
  signal?: AbortSignal,
): Promise<SyncChanges> => {
  const response = await axios.get<WireSyncChanges>("/api/v1/sync/changes", {
    params: { cursor: cursor ?? undefined },
    signal,
  });
  return {
    records: response.data.records.map(toSyncRecord),
    nextCursor: response.data.nextCursor,
    hasMore: response.data.hasMore,
  };
};

export const createSyncRecords = async (
  request: { records: CreateSyncRecord[] },
  signal?: AbortSignal,
  antiforgeryToken?: string,
): Promise<SyncRecord[]> =>
  (
    signal === undefined && antiforgeryToken === undefined
      ? await axios.post<{ records: WireSyncRecord[] }>("/api/v1/sync/records", {
          records: request.records.map(toWireCreateRecord),
        })
      : await axios.post<{ records: WireSyncRecord[] }>(
          "/api/v1/sync/records",
          { records: request.records.map(toWireCreateRecord) },
          mutationConfig(signal, antiforgeryToken),
        )
  ).data.records.map(toSyncRecord);

export const replaceSyncRecord = async (
  id: string,
  request: ReplaceSyncRecord,
  signal?: AbortSignal,
  antiforgeryToken?: string,
): Promise<SyncRecord> =>
  {
    try {
      return toSyncRecord(
        (
          signal === undefined && antiforgeryToken === undefined
            ? await axios.put<WireSyncRecord>(
                `/api/v1/sync/records/${encodeURIComponent(id)}`,
                toWireReplaceRecord(request),
              )
            : await axios.put<WireSyncRecord>(
                `/api/v1/sync/records/${encodeURIComponent(id)}`,
                toWireReplaceRecord(request),
                mutationConfig(signal, antiforgeryToken),
              )
        ).data,
      );
    } catch (error) {
      if (axios.isAxiosError(error) && error.response?.status === 409) {
        throw new SyncConflictError(toSyncRecord(error.response.data as WireSyncRecord));
      }
      throw error;
    }
  };

export const deleteSyncRecord = async (
  id: string,
  signal?: AbortSignal,
  antiforgeryToken?: string,
): Promise<void> => {
  if (signal === undefined && antiforgeryToken === undefined) {
    await axios.delete(`/api/v1/sync/records/${encodeURIComponent(id)}`);
  } else {
    await axios.delete(
      `/api/v1/sync/records/${encodeURIComponent(id)}`,
      mutationConfig(signal, antiforgeryToken),
    );
  }
};

export class SyncClient {
  constructor(private readonly database: VaultDatabase) {}

  async pull(signal?: AbortSignal): Promise<void> {
    let cursor = (await this.database.getSyncState())?.cursor ?? null;
    let hasMore = true;

    while (hasMore) {
      assertNotAborted(signal);
      const page = await getSyncChanges(cursor, signal);
      assertNotAborted(signal);
      const mutations: VaultSyncMutation[] = [];
      for (const record of page.records) {
        assertNotAborted(signal);
        mutations.push(...await this.prepare(record, signal));
      }
      assertNotAborted(signal);
      await this.database.applySyncPage(mutations, page.nextCursor, signal);
      cursor = page.nextCursor;
      hasMore = page.hasMore;
    }
  }

  private async prepare(
    record: SyncRecord,
    signal: AbortSignal | undefined,
  ): Promise<VaultSyncMutation[]> {
    const stored: VaultRecord = { ...record };
    const [current, pending] = await Promise.all([
      this.database.getRecord(record.id),
      this.database.outboxEntryForRecord(record.id),
    ]);
    assertNotAborted(signal);
    if (current !== undefined && current.revision > stored.revision) return [];

    if (!stored.tombstone) {
      try {
        decodeVaultPayloadV1(stored.recordType, stored.id, stored.payload);
      } catch {
        return [{
          kind: "put-quarantine",
          entry: {
            recordId: stored.id,
            revision: stored.revision,
            record: stored,
            reason: "invalid-payload",
          },
        }];
      }
    }

    if (pending !== undefined) {
      return [
        {
          kind: "stage-outbox-server-record",
          operationId: pending.operationId,
          record: stored,
        },
        { kind: "delete-quarantine", recordId: stored.id, revision: stored.revision },
      ];
    }
    return [
      { kind: "put-record", record: stored },
      { kind: "delete-quarantine", recordId: stored.id, revision: stored.revision },
    ];
  }
}
