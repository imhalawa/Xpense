import { PROTOCOL_VERSION, type RecordType } from "../crypto/protocol";

export const VAULT_DATABASE_NAME = "xpense-vault";
export const LEGACY_QUARANTINE_REVISION = 0;

const vaultDatabaseVersion = 3;

export type VaultStoreName =
  | "records"
  | "envelopes"
  | "wrappers"
  | "syncState"
  | "outbox"
  | "quarantine";

const storeDefinitions: ReadonlyArray<{
  name: VaultStoreName;
  keyPath: string | string[];
}> = [
  { name: "records", keyPath: "id" },
  { name: "envelopes", keyPath: ["recordId", "groupId"] },
  { name: "wrappers", keyPath: "id" },
  { name: "syncState", keyPath: "key" },
  { name: "outbox", keyPath: "operationId" },
  { name: "quarantine", keyPath: ["recordId", "revision"] },
];

export interface VaultRecordEnvelope {
  id: string;
  groupId: string | null;
  wrappedKey: Uint8Array;
  nonce: Uint8Array;
  encapsulatedKey: Uint8Array | null;
  protocolVersion: number;
}

export interface VaultRecord {
  id: string;
  recordType: RecordType;
  ownerId: string;
  parentResourceId: string | null;
  revision: number;
  protocolVersion: number;
  nonce: Uint8Array;
  ciphertext: Uint8Array;
  envelopes: VaultRecordEnvelope[];
  tombstone: boolean;
  sequenceNumber: number;
  serverCreatedAt: string;
  serverUpdatedAt: string;
}

export interface VaultSyncState {
  key: "changes";
  cursor: string | null;
}

export interface VaultQuarantineEntry {
  recordId: string;
  revision: number;
  record?: VaultRecord;
  ciphertext?: Uint8Array;
  reason: "authentication-failed" | "invalid-payload";
}

export interface VaultOutboxCreateRequest {
  id: string;
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

export interface VaultOutboxReplaceRequest {
  expectedRevision: number;
  protocolVersion: number;
  nonce: Uint8Array;
  ciphertext: Uint8Array;
}

export type VaultOutboxMutation =
  | { kind: "create"; record: VaultRecord; request: VaultOutboxCreateRequest }
  | { kind: "replace"; record: VaultRecord; request: VaultOutboxReplaceRequest }
  | { kind: "delete"; record: VaultRecord };

export interface VaultOutboxEntry {
  operationId: string;
  idempotencyKey: string;
  sequence: number;
  mutation: VaultOutboxMutation;
  latestServerRecord?: VaultRecord;
}

export type VaultSyncMutation =
  | { kind: "put-record"; record: VaultRecord }
  | { kind: "put-quarantine"; entry: VaultQuarantineEntry }
  | { kind: "delete-quarantine"; recordId: string; revision: number }
  | { kind: "stage-outbox-server-record"; operationId: string; record: VaultRecord };

interface StoredVaultRecord {
  id: string;
  recordType?: RecordType;
  type?: RecordType;
  ownerId: string;
  parentResourceId?: string | null;
  revision: number;
  protocolVersion?: number;
  nonce: Uint8Array;
  ciphertext: Uint8Array;
  envelopes?: VaultRecordEnvelope[];
  tombstone: boolean;
  sequenceNumber?: number;
  serverCreatedAt: string;
  serverUpdatedAt: string;
}

interface StoredVaultEnvelope {
  id?: string;
  recordId: string;
  groupId: string | null;
  wrappedKey: Uint8Array;
  nonce: Uint8Array;
  encapsulatedKey?: Uint8Array | null;
  protocolVersion: number;
}

const normalizeEnvelope = (stored: StoredVaultEnvelope): VaultRecordEnvelope => ({
  id: stored.id ?? `${stored.recordId}:${stored.groupId ?? "personal"}`,
  groupId: stored.groupId === "personal" ? null : stored.groupId,
  wrappedKey: stored.wrappedKey,
  nonce: stored.nonce,
  encapsulatedKey: stored.encapsulatedKey ?? null,
  protocolVersion: stored.protocolVersion,
});

const normalizeVaultRecord = (
  stored: StoredVaultRecord,
  separateEnvelopes: readonly StoredVaultEnvelope[],
): VaultRecord => {
  const recordType = stored.recordType ?? stored.type;
  if (recordType === undefined) throw new Error("The stored vault record type is missing");
  const envelopes = stored.envelopes ?? separateEnvelopes
    .filter((envelope) => envelope.recordId === stored.id)
    .map(normalizeEnvelope);
  return {
    id: stored.id,
    recordType,
    ownerId: stored.ownerId,
    parentResourceId: stored.parentResourceId ?? null,
    revision: stored.revision,
    protocolVersion:
      stored.protocolVersion ?? envelopes[0]?.protocolVersion ?? PROTOCOL_VERSION,
    nonce: stored.nonce,
    ciphertext: stored.ciphertext,
    envelopes,
    tombstone: stored.tombstone,
    sequenceNumber: stored.sequenceNumber ?? 0,
    serverCreatedAt: stored.serverCreatedAt,
    serverUpdatedAt: stored.serverUpdatedAt,
  };
};

const assertNotAborted = (signal: AbortSignal | undefined): void => {
  if (signal?.aborted) throw new Error("The sync was cancelled");
};

const runTransaction = <T>(
  database: IDBDatabase,
  storeName: VaultStoreName,
  mode: IDBTransactionMode,
  operation: (store: IDBObjectStore) => IDBRequest<T>,
): Promise<T> =>
  new Promise((resolve, reject) => {
    const transaction = database.transaction(storeName, mode);
    const request = operation(transaction.objectStore(storeName));
    let result: T;
    request.onsuccess = () => {
      result = request.result;
    };
    transaction.oncomplete = () => resolve(result);
    transaction.onerror = () => reject(transaction.error ?? request.error);
    transaction.onabort = () => reject(transaction.error ?? request.error);
  });

export class VaultDatabase {
  constructor(private readonly database: IDBDatabase) {}

  get objectStoreNames(): DOMStringList {
    return this.database.objectStoreNames;
  }

  get version(): number {
    return this.database.version;
  }

  put(storeName: VaultStoreName, value: object): Promise<IDBValidKey> {
    return runTransaction(this.database, storeName, "readwrite", (store) => store.put(value));
  }

  async get<T = Record<string, unknown>>(
    storeName: VaultStoreName,
    key: IDBValidKey,
  ): Promise<T | undefined> {
    return runTransaction<T | undefined>(this.database, storeName, "readonly", (store) =>
      store.get(key),
    );
  }

  async delete(storeName: VaultStoreName, key: IDBValidKey): Promise<void> {
    await runTransaction(this.database, storeName, "readwrite", (store) => store.delete(key));
  }

  putRecord(record: VaultRecord): Promise<IDBValidKey> {
    return this.put("records", record);
  }

  async getRecord(id: string): Promise<VaultRecord | undefined> {
    const [stored, envelopes] = await Promise.all([
      this.get<StoredVaultRecord>("records", id),
      runTransaction<StoredVaultEnvelope[]>(this.database, "envelopes", "readonly", (store) =>
        store.getAll(),
      ),
    ]);
    return stored === undefined ? undefined : normalizeVaultRecord(stored, envelopes);
  }

  async records(): Promise<VaultRecord[]> {
    const [stored, envelopes] = await Promise.all([
      runTransaction<StoredVaultRecord[]>(this.database, "records", "readonly", (store) =>
        store.getAll(),
      ),
      runTransaction<StoredVaultEnvelope[]>(this.database, "envelopes", "readonly", (store) =>
        store.getAll(),
      ),
    ]);
    return stored.map((record) => normalizeVaultRecord(record, envelopes));
  }

  getSyncState(): Promise<VaultSyncState | undefined> {
    return this.get<VaultSyncState>("syncState", "changes");
  }

  putSyncState(state: VaultSyncState): Promise<IDBValidKey> {
    return this.put("syncState", state);
  }

  async enqueueOutbox(
    entry: Omit<VaultOutboxEntry, "sequence">,
  ): Promise<VaultOutboxEntry> {
    return (await this.enqueueOutboxBatch([entry]))[0]!;
  }

  async enqueueOutboxBatch(
    entries: readonly Omit<VaultOutboxEntry, "sequence">[],
  ): Promise<VaultOutboxEntry[]> {
    return new Promise((resolve, reject) => {
      const transaction = this.database.transaction(["outbox", "records"], "readwrite");
      const outbox = transaction.objectStore("outbox");
      const records = transaction.objectStore("records");
      const request = outbox.getAll();
      let queued: VaultOutboxEntry[] = [];
      request.onsuccess = () => {
        let sequence = (request.result as VaultOutboxEntry[]).reduce(
          (highest, queued) => Math.max(highest, queued.sequence ?? 0),
          0,
        ) + 1;
        try {
          queued = entries.map((entry) => {
            const value = { ...entry, sequence };
            sequence += 1;
            outbox.put(value);
            records.put(entry.mutation.record);
            return value;
          });
        } catch (error) {
          transaction.abort();
          reject(error);
        }
      };
      transaction.oncomplete = () => resolve(queued);
      transaction.onerror = () => reject(transaction.error ?? request.error);
      transaction.onabort = () => reject(transaction.error ?? request.error);
    });
  }

  async enqueueOutboxOnce(
    entry: Omit<VaultOutboxEntry, "sequence">,
  ): Promise<VaultOutboxEntry> {
    return new Promise((resolve, reject) => {
      const transaction = this.database.transaction(["outbox", "records"], "readwrite");
      const outbox = transaction.objectStore("outbox");
      const records = transaction.objectStore("records");
      const existingRequest = outbox.get(entry.operationId);
      let queued: VaultOutboxEntry;
      existingRequest.onsuccess = () => {
        const existing = existingRequest.result as VaultOutboxEntry | undefined;
        if (existing !== undefined) {
          queued = existing;
          return;
        }
        const entriesRequest = outbox.getAll();
        entriesRequest.onsuccess = () => {
          const sequence = (entriesRequest.result as VaultOutboxEntry[]).reduce(
            (highest, current) => Math.max(highest, current.sequence ?? 0),
            0,
          ) + 1;
          queued = { ...entry, sequence };
          outbox.put(queued);
          records.put(entry.mutation.record);
        };
      };
      transaction.oncomplete = () => resolve(queued!);
      transaction.onerror = () => reject(transaction.error ?? existingRequest.error);
      transaction.onabort = () => reject(transaction.error ?? existingRequest.error);
    });
  }

  outboxEntries(): Promise<VaultOutboxEntry[]> {
    return runTransaction<VaultOutboxEntry[]>(this.database, "outbox", "readonly", (store) =>
      store.getAll(),
    ).then((entries) => entries.sort((left, right) => left.sequence - right.sequence));
  }

  async outboxEntryForRecord(recordId: string): Promise<VaultOutboxEntry | undefined> {
    return (await this.outboxEntries()).find((entry) => entry.mutation.record.id === recordId);
  }

  async replaceOutbox(entry: VaultOutboxEntry): Promise<void> {
    await new Promise<void>((resolve, reject) => {
      const transaction = this.database.transaction(["outbox", "records"], "readwrite");
      transaction.objectStore("outbox").put(entry);
      transaction.objectStore("records").put(entry.mutation.record);
      transaction.oncomplete = () => resolve();
      transaction.onerror = () => reject(transaction.error);
      transaction.onabort = () => reject(transaction.error);
    });
  }

  async acknowledgeOutbox(operationId: string, record?: VaultRecord): Promise<void> {
    await new Promise<void>((resolve, reject) => {
      const stores = record === undefined ? ["outbox"] : ["outbox", "records"];
      const transaction = this.database.transaction(stores, "readwrite");
      transaction.objectStore("outbox").delete(operationId);
      if (record !== undefined) transaction.objectStore("records").put(record);
      transaction.oncomplete = () => resolve();
      transaction.onerror = () => reject(transaction.error);
      transaction.onabort = () => reject(transaction.error);
    });
  }

  putQuarantine(entry: VaultQuarantineEntry): Promise<IDBValidKey> {
    return this.put("quarantine", entry);
  }

  getQuarantine(recordId: string, revision: number): Promise<VaultQuarantineEntry | undefined> {
    return this.get<VaultQuarantineEntry>("quarantine", [recordId, revision]);
  }

  quarantineEntries(): Promise<VaultQuarantineEntry[]> {
    return runTransaction<VaultQuarantineEntry[]>(
      this.database,
      "quarantine",
      "readonly",
      (store) => store.getAll(),
    );
  }

  applySyncPage(
    mutations: readonly VaultSyncMutation[],
    cursor: string,
    signal?: AbortSignal,
  ): Promise<void> {
    assertNotAborted(signal);
    return new Promise((resolve, reject) => {
      const transaction = this.database.transaction(
        ["records", "outbox", "quarantine", "syncState"],
        "readwrite",
      );
      const records = transaction.objectStore("records");
      const outbox = transaction.objectStore("outbox");
      const quarantine = transaction.objectStore("quarantine");
      const syncState = transaction.objectStore("syncState");
      const abort = (): void => {
        try {
          transaction.abort();
        } catch {
          return;
        }
      };
      const cleanUp = (): void => signal?.removeEventListener("abort", abort);
      signal?.addEventListener("abort", abort, { once: true });
      transaction.oncomplete = () => {
        cleanUp();
        resolve();
      };
      transaction.onerror = () => {
        cleanUp();
        reject(transaction.error);
      };
      transaction.onabort = () => {
        cleanUp();
        reject(signal?.aborted ? new Error("The sync was cancelled") : transaction.error);
      };

      try {
        for (const mutation of mutations) {
          assertNotAborted(signal);
          if (mutation.kind === "put-record") records.put(mutation.record);
          else if (mutation.kind === "put-quarantine") quarantine.put(mutation.entry);
          else if (mutation.kind === "delete-quarantine") {
            quarantine.delete([mutation.recordId, mutation.revision]);
          } else {
            const request = outbox.get(mutation.operationId);
            request.onsuccess = () => {
              const entry = request.result as VaultOutboxEntry | undefined;
              if (entry?.mutation.record.id === mutation.record.id) {
                outbox.put({ ...entry, latestServerRecord: mutation.record });
              }
            };
          }
        }
        assertNotAborted(signal);
        syncState.put({ key: "changes", cursor });
      } catch {
        abort();
      }
    });
  }

  close(): void {
    this.database.close();
  }
}

export const openVaultDatabase = (): Promise<VaultDatabase> =>
  new Promise((resolve, reject) => {
    const request = indexedDB.open(VAULT_DATABASE_NAME, vaultDatabaseVersion);
    request.onupgradeneeded = (event) => {
      const database = request.result;
      const recreateQuarantine =
        event.oldVersion < 2 && database.objectStoreNames.contains("quarantine");
      if (recreateQuarantine) {
        const existing = request.transaction!.objectStore("quarantine").getAll();
        existing.onsuccess = () => {
          database.deleteObjectStore("quarantine");
          const quarantine = database.createObjectStore("quarantine", {
            keyPath: ["recordId", "revision"],
          });
          for (const value of existing.result as Array<Record<string, unknown>>) {
            const failedRecord = value.record as { revision?: unknown } | undefined;
            const revision =
              typeof value.revision === "number" ? value.revision : failedRecord?.revision;
            quarantine.put({
              ...value,
              revision: typeof revision === "number" ? revision : LEGACY_QUARANTINE_REVISION,
            });
          }
        };
      }
      for (const definition of storeDefinitions) {
        if (definition.name === "quarantine" && recreateQuarantine) continue;
        if (!database.objectStoreNames.contains(definition.name)) {
          database.createObjectStore(definition.name, { keyPath: definition.keyPath });
        }
      }
      if (event.oldVersion < 3) {
        const outbox = request.transaction!.objectStore("outbox");
        if (!outbox.indexNames.contains("sequence")) {
          outbox.createIndex("sequence", "sequence", { unique: true });
        }
        const existing = outbox.getAll();
        existing.onsuccess = () => {
          let nextSequence = (existing.result as Array<Record<string, unknown>>).reduce(
            (highest, value) =>
              typeof value.sequence === "number" ? Math.max(highest, value.sequence) : highest,
            0,
          ) + 1;
          for (const value of existing.result as Array<Record<string, unknown>>) {
            if (typeof value.sequence !== "number") {
              outbox.put({ ...value, sequence: nextSequence });
              nextSequence += 1;
            }
          }
        };
      }
    };
    request.onsuccess = () => resolve(new VaultDatabase(request.result));
    request.onerror = () => reject(request.error);
    request.onblocked = () => reject(new Error("The vault database upgrade is blocked"));
  });

export const deleteVaultDatabase = (): Promise<void> =>
  new Promise((resolve, reject) => {
    const request = indexedDB.deleteDatabase(VAULT_DATABASE_NAME);
    request.onsuccess = () => resolve();
    request.onerror = () => reject(request.error);
    request.onblocked = () => reject(new Error("The vault database deletion is blocked"));
  });
