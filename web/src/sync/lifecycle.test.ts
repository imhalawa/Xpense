import { describe, expect, it, vi } from "vitest";
import type { VaultDatabase } from "../vault/vaultDatabase";
import { SyncLifecycleCoordinator } from "./lifecycle";
import type { OutboxMutationBuilder } from "./outbox";

describe("SyncLifecycleCoordinator", () => {
  it("wires each outbox manager to the lifecycle conflict memory", () => {
    const lifecycle = new SyncLifecycleCoordinator();
    const mutations: OutboxMutationBuilder = { build: vi.fn() };
    const manager = lifecycle.createOutboxManager(
      {} as VaultDatabase,
      mutations,
      { apply: vi.fn() },
    );

    expect(manager.conflicts).toBe(lifecycle.conflicts);
  });

  it("clears conflicts on lock", () => {
    const lifecycle = new SyncLifecycleCoordinator();
    const clear = vi.spyOn(lifecycle.conflicts, "clear");

    lifecycle.lock();

    expect(clear).toHaveBeenCalledOnce();
    expect(lifecycle.conflicts.entries()).toEqual([]);
  });
});
