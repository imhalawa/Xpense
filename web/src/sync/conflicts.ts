import type { VaultOutboxEntry, VaultRecord } from "../vault/vaultDatabase";

export interface SyncConflict {
  recordId: string;
  outboxEntry: VaultOutboxEntry;
  local: VaultRecord;
  latest: VaultRecord;
  mine: Uint8Array;
  theirs: Uint8Array;
}

export class ConflictManager {
  private readonly conflicts = new Map<string, SyncConflict>();

  add(conflict: SyncConflict): void {
    this.conflicts.set(conflict.recordId, conflict);
  }

  get(recordId: string): SyncConflict | undefined {
    return this.conflicts.get(recordId);
  }

  remove(recordId: string): void {
    this.conflicts.delete(recordId);
  }

  entries(): readonly SyncConflict[] {
    return [...this.conflicts.values()];
  }

  clear(): void {
    this.conflicts.clear();
  }
}
