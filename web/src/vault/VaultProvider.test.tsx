import { afterEach, describe, expect, it, vi } from "vitest";
import { act, render, renderHook, screen } from "@testing-library/react";
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
  const { state, rowCeiling } = useVault();
  return (
    <div>
      <span data-testid="state">{state}</span>
      <span data-testid="ceiling">
        {rowCeiling === null ? "none" : String(rowCeiling.loadedRowCount)}
      </span>
    </div>
  );
};

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

    expect(screen.getByTestId("state").textContent).toBe("ready");
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
    expect(screen.getByTestId("state").textContent).toBe("ready");
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
});
