export const VAULT_DATABASE_NAME = "xpense-vault";

const vaultDatabaseVersion = 4;

export const recordTypes = [
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
] as const;

export type RecordType = (typeof recordTypes)[number];

export type VaultStoreName = "records" | "syncState" | "outbox" | "quarantine";

const storeDefinitions: ReadonlyArray<{
  name: VaultStoreName;
  keyPath: string | string[];
}> = [
  { name: "records", keyPath: "id" },
  { name: "syncState", keyPath: "key" },
  { name: "outbox", keyPath: "operationId" },
  { name: "quarantine", keyPath: ["recordId", "revision"] },
];

export interface VaultRecord {
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

export interface VaultSyncState {
  key: "changes";
  cursor: string | null;
}

export interface VaultQuarantineEntry {
  recordId: string;
  revision: number;
  record?: VaultRecord;
  reason: "invalid-payload";
}

export interface VaultOutboxCreateRequest {
  id: string;
  recordType: number;
  parentResourceId: string | null;
  payload: Uint8Array;
}

export interface VaultOutboxReplaceRequest {
  expectedRevision: number;
  payload: Uint8Array;
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

  getRecord(id: string): Promise<VaultRecord | undefined> {
    return this.get<VaultRecord>("records", id);
  }

  records(): Promise<VaultRecord[]> {
    return runTransaction<VaultRecord[]>(this.database, "records", "readonly", (store) =>
      store.getAll(),
    );
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
    request.onupgradeneeded = () => {
      const database = request.result;
      for (const existing of Array.from(database.objectStoreNames)) {
        database.deleteObjectStore(existing);
      }
      for (const definition of storeDefinitions) {
        database.createObjectStore(definition.name, { keyPath: definition.keyPath });
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
