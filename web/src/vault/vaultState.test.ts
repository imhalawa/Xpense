import { afterEach, describe, expect, it, vi } from "vitest";
import {
  HIDDEN_LOCK_MILLISECONDS,
  IDLE_LOCK_MILLISECONDS,
  VaultStateMachine,
} from "./vaultState";

afterEach(() => {
  vi.useRealTimers();
});

describe("vault lock state machine", () => {
  it("stays unlocked at 14:59 and locks at 15:00 of inactivity", () => {
    vi.useFakeTimers();
    const onLock = vi.fn();
    const machine = new VaultStateMachine(onLock);
    machine.startUnlock();
    machine.completeUnlock();

    vi.advanceTimersByTime(IDLE_LOCK_MILLISECONDS - 1_000);
    expect(machine.state).toBe("unlocked");
    vi.advanceTimersByTime(1_000);

    expect(machine.state).toBe("locked");
    expect(onLock).toHaveBeenCalledOnce();
  });

  it("pushes the idle lock forward after an interaction", () => {
    vi.useFakeTimers();
    const machine = new VaultStateMachine(vi.fn());
    machine.startUnlock();
    machine.completeUnlock();

    vi.advanceTimersByTime(14 * 60 * 1_000);
    machine.interaction();
    vi.advanceTimersByTime(IDLE_LOCK_MILLISECONDS - 1);
    expect(machine.state).toBe("unlocked");
    vi.advanceTimersByTime(1);

    expect(machine.state).toBe("locked");
  });

  it("locks after fifteen continuously hidden minutes independently of idle activity", () => {
    vi.useFakeTimers();
    const machine = new VaultStateMachine(vi.fn());
    machine.startUnlock();
    machine.completeUnlock();
    vi.advanceTimersByTime(14 * 60 * 1_000);
    machine.interaction();
    machine.visibilityChanged(true);

    vi.advanceTimersByTime(HIDDEN_LOCK_MILLISECONDS - 1);
    expect(machine.state).toBe("unlocked");
    vi.advanceTimersByTime(1);

    expect(machine.state).toBe("locked");
  });

  it("keeps an unwrap failure locked and allows another attempt", () => {
    const machine = new VaultStateMachine(vi.fn());
    machine.startUnlock();
    machine.failUnlock();

    expect(machine.state).toBe("locked");
    machine.startUnlock();
    expect(machine.state).toBe("unlocking");
  });

  it("keeps a service failure unavailable after clearing keys", () => {
    const machine = new VaultStateMachine(vi.fn());
    machine.startUnlock();
    machine.markUnavailable();

    expect(machine.state).toBe("unavailable");
  });
});
