import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import type { WrapperKind } from "../crypto/protocol";
import { SyncLifecycleCoordinator } from "../sync/lifecycle";
import type { VaultWorkerFactory } from "../crypto/worker/vaultWorkerClient";
import { VaultWorkerClient } from "../crypto/worker/vaultWorkerClient";
import type { RowCeilingAware, RowCeilingReport } from "./plaintextProjection";
import type { VaultProjection, VaultState as ProjectionState } from "./VaultProjection";
import { VaultStateMachine, type VaultLifecycleState } from "./vaultState";

const missingProviderMessage = "useVault must be used inside a VaultProvider";

export interface VaultContextValue {
  projection: VaultProjection;
  state: VaultLifecycleState;
  rowCeiling: RowCeilingReport | null;
  availableWrappers: readonly WrapperKind[];
  sensitiveError: string | null;
  syncLifecycle: SyncLifecycleCoordinator;
  lock(): void;
  unlockWithMasterKey(
    masterKey: CryptoKey,
    userId: string,
    encryptedPrivateKey?: Uint8Array,
  ): Promise<boolean>;
}

const VaultContext = createContext<VaultContextValue | null>(null);

export interface VaultProviderProps {
  projection: VaultProjection & Partial<RowCeilingAware>;
  children: ReactNode;
  workerFactory?: VaultWorkerFactory;
  availableWrappers?: readonly WrapperKind[];
  autoUnlock?: boolean;
  syncLifecycle?: SyncLifecycleCoordinator;
}

export const VaultProvider = ({
  projection,
  children,
  workerFactory,
  availableWrappers = [],
  autoUnlock = true,
  syncLifecycle,
}: VaultProviderProps) => {
  const projectionRef = useRef(projection);
  const defaultSyncLifecycleRef = useRef<SyncLifecycleCoordinator | null>(null);
  if (defaultSyncLifecycleRef.current === null) {
    defaultSyncLifecycleRef.current = new SyncLifecycleCoordinator();
  }
  const activeSyncLifecycle = syncLifecycle ?? defaultSyncLifecycleRef.current;
  const workerFactoryRef = useRef(workerFactory);
  const workerRef = useRef<VaultWorkerClient | null>(null);
  const ignoreProjectionLockRef = useRef(false);
  const syncLifecycleRef = useRef(activeSyncLifecycle);
  const [sensitiveError, setSensitiveError] = useState<string | null>(null);
  projectionRef.current = projection;
  workerFactoryRef.current = workerFactory;
  syncLifecycleRef.current = activeSyncLifecycle;

  const machineRef = useRef<VaultStateMachine | null>(null);
  if (machineRef.current === null) {
    machineRef.current = new VaultStateMachine((cleanupState) => {
      workerRef.current?.terminate();
      workerRef.current = null;
      syncLifecycleRef.current?.lock();
      ignoreProjectionLockRef.current = cleanupState === "unavailable";
      projectionRef.current.lock();
      ignoreProjectionLockRef.current = false;
      setSensitiveError(null);
    });
  }
  const machine = machineRef.current;
  const [state, setState] = useState<VaultLifecycleState>(machine.state);

  const ensureWorker = useCallback((): VaultWorkerClient | null => {
    if (workerRef.current !== null) return workerRef.current;
    if (workerFactoryRef.current === undefined && typeof Worker === "undefined") return null;
    workerRef.current = new VaultWorkerClient(workerFactoryRef.current);
    return workerRef.current;
  }, []);

  useEffect(() => {
    const updateFromProjection = (projectionState: ProjectionState): void => {
      switch (projectionState) {
        case "locked":
          if (ignoreProjectionLockRef.current) break;
          machine.lock();
          break;
        case "loading":
          machine.startUnlock();
          break;
        case "ready":
          if (machine.state !== "unlocking") machine.startUnlock();
          machine.completeUnlock();
          break;
        case "error":
          machine.markUnavailable();
          break;
      }
    };

    const unsubscribeMachine = machine.subscribe(setState);
    const unsubscribeProjection = projection.subscribe(updateFromProjection);
    const interaction = (): void => machine.interaction();
    const visibilityChanged = (): void => machine.visibilityChanged(document.hidden);
    window.addEventListener("pointerdown", interaction);
    window.addEventListener("keydown", interaction);
    document.addEventListener("visibilitychange", visibilityChanged);

    ensureWorker();
    if (projection.state === "locked" && autoUnlock) {
      machine.startUnlock();
      void projection.unlock().catch(() => machine.markUnavailable());
    } else {
      updateFromProjection(projection.state);
    }

    return () => {
      unsubscribeMachine();
      unsubscribeProjection();
      window.removeEventListener("pointerdown", interaction);
      window.removeEventListener("keydown", interaction);
      document.removeEventListener("visibilitychange", visibilityChanged);
      machine.dispose();
      syncLifecycleRef.current?.lock();
      workerRef.current?.terminate();
      workerRef.current = null;
    };
  }, [autoUnlock, ensureWorker, machine, projection]);

  const lock = useCallback(() => machine.lock(), [machine]);

  const unlockWithMasterKey = useCallback(
    async (
      masterKey: CryptoKey,
      userId: string,
      encryptedPrivateKey?: Uint8Array,
    ): Promise<boolean> => {
      machine.startUnlock();
      setSensitiveError(null);
      const worker = ensureWorker();
      if (worker === null) {
        machine.failUnlock();
        return false;
      }
      try {
        const response = await worker.request({
          type: "unlockWithMasterKey",
          masterKey,
          userId,
          encryptedPrivateKey,
        });
        if (!response.ok) {
          setSensitiveError(response.error.message);
          machine.failUnlock();
          return false;
        }
        if (projection.state !== "ready") await projection.unlock();
        machine.completeUnlock();
        return true;
      } catch (unlockFailure) {
        setSensitiveError(
          unlockFailure instanceof Error ? unlockFailure.message : "The vault could not unlock",
        );
        machine.failUnlock();
        return false;
      }
    },
    [ensureWorker, machine, projection],
  );

  const value = useMemo<VaultContextValue>(
    () => ({
      projection,
      state,
      rowCeiling: projection.rowCeiling ?? null,
      availableWrappers,
      sensitiveError,
      syncLifecycle: activeSyncLifecycle,
      lock,
      unlockWithMasterKey,
    }),
    [activeSyncLifecycle, availableWrappers, lock, projection, sensitiveError, state, unlockWithMasterKey],
  );

  return <VaultContext.Provider value={value}>{children}</VaultContext.Provider>;
};

export const useVault = (): VaultContextValue => {
  const value = useContext(VaultContext);
  if (value === null) throw new Error(missingProviderMessage);
  return value;
};
