export const IDLE_LOCK_MILLISECONDS = 15 * 60 * 1_000;
export const HIDDEN_LOCK_MILLISECONDS = 15 * 60 * 1_000;

export type VaultLifecycleState = "locked" | "unlocking" | "unlocked" | "unavailable";

export class VaultStateMachine {
  private currentState: VaultLifecycleState = "locked";
  private idleTimer: ReturnType<typeof setTimeout> | null = null;
  private hiddenTimer: ReturnType<typeof setTimeout> | null = null;
  private readonly listeners = new Set<(state: VaultLifecycleState) => void>();

  constructor(private readonly onLock: (state: "locked" | "unavailable") => void) {}

  get state(): VaultLifecycleState {
    return this.currentState;
  }

  subscribe(listener: (state: VaultLifecycleState) => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  startUnlock(): void {
    this.clearTimers();
    this.moveTo("unlocking");
  }

  completeUnlock(): void {
    if (this.currentState !== "unlocking") return;
    this.moveTo("unlocked");
    this.resetIdleTimer();
  }

  failUnlock(): void {
    this.clearTimers();
    this.moveTo("locked");
    this.onLock("locked");
  }

  markUnavailable(): void {
    this.clearTimers();
    this.moveTo("unavailable");
    this.onLock("unavailable");
  }

  lock(): void {
    this.clearTimers();
    if (this.currentState === "locked") return;
    this.moveTo("locked");
    this.onLock("locked");
  }

  interaction(): void {
    if (this.currentState === "unlocked") this.resetIdleTimer();
  }

  visibilityChanged(hidden: boolean): void {
    this.interaction();
    if (!hidden) {
      this.clearHiddenTimer();
      return;
    }
    this.clearHiddenTimer();
    this.hiddenTimer = setTimeout(() => this.lock(), HIDDEN_LOCK_MILLISECONDS);
  }

  dispose(): void {
    this.clearTimers();
    this.listeners.clear();
  }

  private moveTo(state: VaultLifecycleState): void {
    if (this.currentState === state) return;
    this.currentState = state;
    for (const listener of this.listeners) listener(state);
  }

  private resetIdleTimer(): void {
    if (this.idleTimer !== null) clearTimeout(this.idleTimer);
    this.idleTimer = setTimeout(() => this.lock(), IDLE_LOCK_MILLISECONDS);
  }

  private clearHiddenTimer(): void {
    if (this.hiddenTimer !== null) clearTimeout(this.hiddenTimer);
    this.hiddenTimer = null;
  }

  private clearTimers(): void {
    if (this.idleTimer !== null) clearTimeout(this.idleTimer);
    this.idleTimer = null;
    this.clearHiddenTimer();
  }
}
