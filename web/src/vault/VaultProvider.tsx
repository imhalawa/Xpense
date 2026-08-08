import { createContext, useContext, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import type { RowCeilingAware, RowCeilingReport } from "./plaintextProjection";
import type { VaultProjection, VaultState } from "./VaultProjection";

const missingProviderMessage = "useVault must be used inside a VaultProvider";

export interface VaultContextValue {
  projection: VaultProjection;
  state: VaultState;
  rowCeiling: RowCeilingReport | null;
}

const VaultContext = createContext<VaultContextValue | null>(null);

export interface VaultProviderProps {
  projection: VaultProjection & Partial<RowCeilingAware>;
  children: ReactNode;
}

export const VaultProvider = ({ projection, children }: VaultProviderProps) => {
  const [state, setState] = useState<VaultState>(projection.state);

  useEffect(() => {
    setState(projection.state);
    const unsubscribe = projection.subscribe(setState);
    if (projection.state === "locked") void projection.unlock().catch(() => undefined);
    return unsubscribe;
  }, [projection]);

  const value = useMemo<VaultContextValue>(
    () => ({ projection, state, rowCeiling: projection.rowCeiling ?? null }),
    [projection, state],
  );

  return <VaultContext.Provider value={value}>{children}</VaultContext.Provider>;
};

export const useVault = (): VaultContextValue => {
  const value = useContext(VaultContext);
  if (value === null) throw new Error(missingProviderMessage);
  return value;
};
