import type { VaultDatabase } from "../vault/vaultDatabase";
import { ConflictManager } from "./conflicts";
import {
  OutboxManager,
  type OptimisticProjection,
  type OutboxMutationBuilder,
  type SyncMutationApi,
} from "./outbox";

export class SyncLifecycleCoordinator {
  readonly conflicts = new ConflictManager();

  createOutboxManager(
    database: VaultDatabase,
    mutations: OutboxMutationBuilder,
    projection: OptimisticProjection,
    syncApi?: SyncMutationApi,
  ): OutboxManager {
    return new OutboxManager(database, mutations, projection, this.conflicts, syncApi);
  }

  lock(): void {
    this.conflicts.clear();
  }
}
