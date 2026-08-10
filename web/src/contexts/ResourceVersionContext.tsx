import { createContext, useContext, type ReactNode } from "react";
import type { AccountView } from "../vault/VaultProjection";

export type ResourceAction = "create" | "edit" | "delete";

interface ResourceContextValue {
  version: number;
  openAccount?: (action: ResourceAction, account: AccountView, trigger: HTMLElement) => void;
}

const ResourceContext = createContext<ResourceContextValue>({ version: 0 });

export const ResourceVersionProvider = ({
  version,
  openAccount,
  children,
}: ResourceContextValue & { children: ReactNode }) => (
  <ResourceContext.Provider value={{ version, openAccount }}>{children}</ResourceContext.Provider>
);

export const useResourceVersion = (): number => useContext(ResourceContext).version;

export const useAccountActions = (): ResourceContextValue["openAccount"] =>
  useContext(ResourceContext).openAccount;
