import axios from "axios";
import type { RecordType } from "../crypto/protocol";
import type {
  VaultDatabase,
  VaultRecord,
  VaultRecordEnvelope,
  VaultSyncMutation,
} from "../vault/vaultDatabase";

export interface SyncEnvelope {
  id: string;
  groupId: string | null;
  wrappedKey: Uint8Array;
  nonce: Uint8Array;
  encapsulatedKey: Uint8Array | null;
  protocolVersion: number;
}

export interface SyncRecord {
  id: string;
  recordType: RecordType;
  ownerId: string;
  parentResourceId: string | null;
  revision: number;
  protocolVersion: number;
  nonce: Uint8Array;
  ciphertext: Uint8Array;
  envelopes: SyncEnvelope[];
  tombstone: boolean;
  sequenceNumber: number;
  serverCreatedAt: string;
  serverUpdatedAt: string;
}

type ByteValue = Uint8Array | number[] | string;

interface WireSyncEnvelope {
  id: string;
  groupId: string | null;
  wrappedKey: ByteValue;
  nonce: ByteValue;
  encapsulatedKey: ByteValue | null;
  protocolVersion: number;
}

interface WireSyncRecord {
  id: string;
  recordType: number;
  ownerUserId: string;
  parentResourceId: string | null;
  revision: number;
  protocolVersion: number;
  nonce: ByteValue;
  ciphertext: ByteValue;
  envelopes: WireSyncEnvelope[];
  isDeleted: boolean;
  sequenceNumber: number;
  createdAt: string;
  updatedAt: string;
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
  protocolVersion: number;
  nonce: Uint8Array;
  ciphertext: Uint8Array;
  personalEnvelope: {
    wrappedKey: Uint8Array;
    nonce: Uint8Array;
    protocolVersion: number;
  };
}

export interface ReplaceSyncRecord {
  expectedRevision: number;
  protocolVersion: number;
  nonce: Uint8Array;
  ciphertext: Uint8Array;
}

export interface AddSyncRecordEnvelope {
  groupId: string;
  permission: 0 | 1;
  wrappedKey: Uint8Array;
  nonce: Uint8Array;
  encapsulatedKey: Uint8Array | null;
  protocolVersion: number;
}

export interface VaultRecordDecryptor {
  decrypt(record: VaultRecord): Promise<Uint8Array>;
}

interface WireCreateSyncRecord extends Omit<CreateSyncRecord, "nonce" | "ciphertext" | "personalEnvelope"> {
  nonce: string;
  ciphertext: string;
  personalEnvelope: {
    wrappedKey: string;
    nonce: string;
    protocolVersion: number;
  };
}

interface WireReplaceSyncRecord extends Omit<ReplaceSyncRecord, "nonce" | "ciphertext"> {
  nonce: string;
  ciphertext: string;
}

interface WireAddSyncRecordEnvelope
  extends Omit<AddSyncRecordEnvelope, "wrappedKey" | "nonce" | "encapsulatedKey"> {
  wrappedKey: string;
  nonce: string;
  encapsulatedKey: string | null;
}

const recordTypes: readonly RecordType[] = [
  "account",
  "transaction",
  "transfer",
  "category",
  "merchant",
  "tag",
  "budget",
  "notification",
  "userProfile",
  "necessityScale",
];

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
  protocolVersion: record.protocolVersion,
  nonce: base64(record.nonce),
  ciphertext: base64(record.ciphertext),
  personalEnvelope: {
    wrappedKey: base64(record.personalEnvelope.wrappedKey),
    nonce: base64(record.personalEnvelope.nonce),
    protocolVersion: record.personalEnvelope.protocolVersion,
  },
});

const toWireReplaceRecord = (record: ReplaceSyncRecord): WireReplaceSyncRecord => ({
  expectedRevision: record.expectedRevision,
  protocolVersion: record.protocolVersion,
  nonce: base64(record.nonce),
  ciphertext: base64(record.ciphertext),
});

const toWireEnvelope = (envelope: AddSyncRecordEnvelope): WireAddSyncRecordEnvelope => ({
  groupId: envelope.groupId,
  permission: envelope.permission,
  wrappedKey: base64(envelope.wrappedKey),
  nonce: base64(envelope.nonce),
  encapsulatedKey:
    envelope.encapsulatedKey === null ? null : base64(envelope.encapsulatedKey),
  protocolVersion: envelope.protocolVersion,
});

const toEnvelope = (envelope: WireSyncEnvelope): VaultRecordEnvelope => ({
  id: envelope.id,
  groupId: envelope.groupId,
  wrappedKey: byteValue(envelope.wrappedKey),
  nonce: byteValue(envelope.nonce),
  encapsulatedKey:
    envelope.encapsulatedKey === null ? null : byteValue(envelope.encapsulatedKey),
  protocolVersion: envelope.protocolVersion,
});

const toSyncRecord = (record: WireSyncRecord): SyncRecord => {
  const recordType = recordTypes[record.recordType];
  if (recordType === undefined) throw new Error("The sync record type is not supported");
  return {
    id: record.id,
    recordType,
    ownerId: record.ownerUserId,
    parentResourceId: record.parentResourceId,
    revision: record.revision,
    protocolVersion: record.protocolVersion,
    nonce: byteValue(record.nonce),
    ciphertext: byteValue(record.ciphertext),
    envelopes: record.envelopes.map(toEnvelope),
    tombstone: record.isDeleted,
    sequenceNumber: record.sequenceNumber,
    serverCreatedAt: record.createdAt,
    serverUpdatedAt: record.updatedAt,
  };
};

const toVaultRecord = (record: SyncRecord): VaultRecord => ({
  ...record,
  envelopes: record.envelopes.map((envelope) => ({ ...envelope })),
});

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

export const addSyncRecordEnvelope = async (
  id: string,
  request: AddSyncRecordEnvelope,
): Promise<SyncEnvelope> =>
  toEnvelope(
    (await axios.post<{ envelope: WireSyncEnvelope }>(
      `/api/v1/sync/records/${encodeURIComponent(id)}/envelopes`,
      toWireEnvelope(request),
    )).data.envelope,
  );

export const revokeSyncRecordEnvelope = async (
  id: string,
  groupId: string,
): Promise<{ keyRotationRequired: boolean; warning: string }> =>
  (
    await axios.delete<{ keyRotationRequired: boolean; warning: string }>(
      `/api/v1/sync/records/${encodeURIComponent(id)}/envelopes/${encodeURIComponent(groupId)}`,
    )
  ).data;

export class SyncClient {
  constructor(
    private readonly database: VaultDatabase,
    private readonly decryptor: VaultRecordDecryptor,
  ) {}

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
    const stored = toVaultRecord(record);
    const [current, pending] = await Promise.all([
      this.database.getRecord(record.id),
      this.database.outboxEntryForRecord(record.id),
    ]);
    assertNotAborted(signal);
    if (current !== undefined && current.revision > stored.revision) return [];

    if (stored.tombstone) {
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

    try {
      const plaintext = await this.decryptor.decrypt(stored);
      plaintext.fill(0);
    } catch {
      assertNotAborted(signal);
      return [{
        kind: "put-quarantine",
        entry: {
          recordId: stored.id,
          revision: stored.revision,
          record: stored,
          reason: "authentication-failed",
        },
      }];
    }

    assertNotAborted(signal);
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
