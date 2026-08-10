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
    const previous = this.conflicts.get(conflict.recordId);
    previous?.mine.fill(0);
    previous?.theirs.fill(0);
    this.conflicts.set(conflict.recordId, conflict);
  }

  get(recordId: string): SyncConflict | undefined {
    return this.conflicts.get(recordId);
  }

  remove(recordId: string): void {
    const conflict = this.conflicts.get(recordId);
    conflict?.mine.fill(0);
    conflict?.theirs.fill(0);
    this.conflicts.delete(recordId);
  }

  entries(): readonly SyncConflict[] {
    return [...this.conflicts.values()];
  }

  clear(): void {
    for (const conflict of this.conflicts.values()) {
      conflict.mine.fill(0);
      conflict.theirs.fill(0);
    }
    this.conflicts.clear();
  }
}
