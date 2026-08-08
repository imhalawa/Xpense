export const VAULT_DATABASE_NAME = "xpense-vault";

const vaultDatabaseVersion = 1;

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
  { name: "quarantine", keyPath: "recordId" },
];

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

  put(storeName: VaultStoreName, value: Record<string, unknown>): Promise<IDBValidKey> {
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

  close(): void {
    this.database.close();
  }
}

export const openVaultDatabase = (): Promise<VaultDatabase> =>
  new Promise((resolve, reject) => {
    const request = indexedDB.open(VAULT_DATABASE_NAME, vaultDatabaseVersion);
    request.onupgradeneeded = () => {
      for (const definition of storeDefinitions) {
        if (!request.result.objectStoreNames.contains(definition.name)) {
          request.result.createObjectStore(definition.name, { keyPath: definition.keyPath });
        }
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
