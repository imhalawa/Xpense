import { afterEach, describe, expect, it, vi } from "vitest";
import { act, fireEvent, render, renderHook, screen } from "@testing-library/react";
import { createUserMasterKey } from "../crypto/keyHierarchy";
import type { VaultWorkerResponse } from "../crypto/worker/commands";
import type {
  VaultWorkerMessage,
  VaultWorkerReply,
  WorkerPort,
} from "../crypto/worker/vaultWorkerClient";
import * as vaultDatabase from "./vaultDatabase";
import { VaultProvider, useVault } from "./VaultProvider";
import type { RowCeilingReport } from "./plaintextProjection";
import type { VaultProjection, VaultState } from "./VaultProjection";

const unsupported = () => Promise.reject(new Error("Not part of this test"));

interface StubProjection extends VaultProjection {
  rowCeiling: RowCeilingReport | null;
  unlockCount: number;
  listenerCount: number;
}

const stubProjection = (initialState: VaultState = "ready"): StubProjection => {
  let currentState = initialState;
  const listeners = new Set<(state: VaultState) => void>();

  const moveTo = (state: VaultState): void => {
    currentState = state;
    for (const listener of listeners) listener(state);
  };

  return {
    get state() {
      return currentState;
    },
    get listenerCount() {
      return listeners.size;
    },
    rowCeiling: null,
    unlockCount: 0,
    subscribe(listener) {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    async unlock() {
      this.unlockCount += 1;
      moveTo("ready");
    },
    lock() {
      moveTo("locked");
    },
    listSpaces: unsupported,
    listAccounts: unsupported,
    listTaxonomy: unsupported,
    resolveFilter: unsupported,
    queryTransactions: unsupported,
    saveTransaction: unsupported,
  };
};

const VaultProbe = () => {
  const { availableWrappers, lock, sensitiveError, state, rowCeiling, unlockWithMasterKey } =
    useVault();
  return (
    <div>
      <span data-testid="state">{state}</span>
      <span data-testid="ceiling">
        {rowCeiling === null ? "none" : String(rowCeiling.loadedRowCount)}
      </span>
      <span data-testid="wrappers">{availableWrappers.join(",")}</span>
      <span data-testid="sensitive-error">{sensitiveError ?? "none"}</span>
      <button type="button" onClick={lock}>
        Lock vault
      </button>
      <button
        type="button"
        onClick={() => {
          void createUserMasterKey().then((masterKey) =>
            unlockWithMasterKey(
              masterKey,
              "11111111-1111-4111-8111-111111111111",
            ),
          );
        }}>
        Unlock vault
      </button>
    </div>
  );
};

class ImmediateWorker implements WorkerPort {
  onmessage: ((event: MessageEvent<VaultWorkerReply>) => void) | null = null;
  onerror: ((event: ErrorEvent) => void) | null = null;
  readonly terminate = vi.fn();

  constructor(private readonly response: VaultWorkerResponse) {}

  postMessage(message: VaultWorkerMessage): void {
    queueMicrotask(() => {
      this.onmessage?.(
        new MessageEvent("message", {
          data: { requestId: message.requestId, response: this.response },
        }),
      );
    });
  }
}

describe("VaultProvider", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("names itself in the error a component outside it gets", () => {
    vi.spyOn(console, "error").mockImplementation(() => undefined);

    expect(() => renderHook(() => useVault())).toThrow(
      "useVault must be used inside a VaultProvider",
    );
  });

  it("publishes the projection's current state", () => {
    render(
      <VaultProvider projection={stubProjection()}>
        <VaultProbe />
      </VaultProvider>,
    );

    expect(screen.getByTestId("state").textContent).toBe("unlocked");
  });

  it("unlocks a locked projection once on mount", async () => {
    const projection = stubProjection("locked");

    await act(async () => {
      render(
        <VaultProvider projection={projection}>
          <VaultProbe />
        </VaultProvider>,
      );
    });

    expect(projection.unlockCount).toBe(1);
    expect(screen.getByTestId("state").textContent).toBe("unlocked");
  });

  it("leaves an already unlocked projection alone", () => {
    const projection = stubProjection();

    render(
      <VaultProvider projection={projection}>
        <VaultProbe />
      </VaultProvider>,
    );

    expect(projection.unlockCount).toBe(0);
  });

  it("re-renders when the projection locks", () => {
    const projection = stubProjection();

    render(
      <VaultProvider projection={projection}>
        <VaultProbe />
      </VaultProvider>,
    );

    act(() => {
      projection.lock();
    });

    expect(screen.getByTestId("state").textContent).toBe("locked");
  });

  it("surfaces the row ceiling a projection reports", () => {
    const projection = stubProjection();
    projection.rowCeiling = {
      loadedRowCount: 2000,
      availableRowCount: 4000,
      reachedCeiling: true,
    };

    render(
      <VaultProvider projection={projection}>
        <VaultProbe />
      </VaultProvider>,
    );

    expect(screen.getByTestId("ceiling").textContent).toBe("2000");
  });

  it("reports no ceiling for a projection that does not track one", () => {
    render(
      <VaultProvider projection={stubProjection()}>
        <VaultProbe />
      </VaultProvider>,
    );

    expect(screen.getByTestId("ceiling").textContent).toBe("none");
  });

  it("stops listening when it unmounts", () => {
    const projection = stubProjection();

    const view = render(
      <VaultProvider projection={projection}>
        <VaultProbe />
      </VaultProvider>,
    );
    expect(projection.listenerCount).toBe(1);

    view.unmount();

    expect(projection.listenerCount).toBe(0);
  });

  it("manual lock terminates the worker and clears the decrypted projection", () => {
    const projection = stubProjection();
    const worker = new ImmediateWorker({ ok: true, value: { unlocked: true } });

    render(
      <VaultProvider projection={projection} workerFactory={() => worker}>
        <VaultProbe />
      </VaultProvider>,
    );
    fireEvent.click(screen.getByRole("button", { name: "Lock vault" }));

    expect(worker.terminate).toHaveBeenCalledOnce();
    expect(projection.state).toBe("locked");
    expect(screen.getByTestId("state").textContent).toBe("locked");
  });

  it("keeps a failed unwrap locked and offers another wrapper", async () => {
    const worker = new ImmediateWorker({
      ok: false,
      error: { code: "operation-failed", message: "sensitive decrypt detail" },
    });

    render(
      <VaultProvider
        projection={stubProjection()}
        workerFactory={() => worker}
        availableWrappers={["recoveryPassword", "recoveryFile"]}>
        <VaultProbe />
      </VaultProvider>,
    );
    fireEvent.click(screen.getByRole("button", { name: "Unlock vault" }));
    await act(async () => undefined);

    expect(screen.getByTestId("state").textContent).toBe("locked");
    expect(screen.getByTestId("wrappers").textContent).toBe(
      "recoveryPassword,recoveryFile",
    );
    expect(screen.getByTestId("sensitive-error").textContent).toBe("none");
  });

  it("never restores an unlocked key from the vault database on reload", () => {
    const openDatabase = vi.spyOn(vaultDatabase, "openVaultDatabase");

    render(
      <VaultProvider projection={stubProjection("locked")} autoUnlock={false}>
        <VaultProbe />
      </VaultProvider>,
    );

    expect(screen.getByTestId("state").textContent).toBe("locked");
    expect(openDatabase).not.toHaveBeenCalled();
  });
});
