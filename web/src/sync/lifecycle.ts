import type { VaultDatabase } from "../vault/vaultDatabase";
import { ConflictManager } from "./conflicts";
import {
  OutboxManager,
  type OptimisticProjection,
  type OutboxCipher,
  type SyncMutationApi,
} from "./outbox";

export class SyncLifecycleCoordinator {
  readonly conflicts = new ConflictManager();

  createOutboxManager(
    database: VaultDatabase,
    cipher: OutboxCipher,
    projection: OptimisticProjection,
    syncApi?: SyncMutationApi,
  ): OutboxManager {
    return new OutboxManager(database, cipher, projection, this.conflicts, syncApi);
  }

  lock(): void {
    this.conflicts.clear();
  }
}
