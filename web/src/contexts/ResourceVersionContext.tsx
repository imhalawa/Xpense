import { createContext, useContext, type ReactNode } from "react";

const ResourceVersionContext = createContext(0);

export const ResourceVersionProvider = ({
  version,
  children,
}: {
  version: number;
  children: ReactNode;
}) => (
  <ResourceVersionContext.Provider value={version}>{children}</ResourceVersionContext.Provider>
);

export const useResourceVersion = (): number => useContext(ResourceVersionContext);
