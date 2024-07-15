import { createContext, useContext, useState } from "react";

interface ILoadingContext {
  loading: boolean;
  setLoading: React.Dispatch<React.SetStateAction<boolean>>;
}

const LoadingContext = createContext({} as ILoadingContext);

export const LoadingProvider = ({ children }: any) => {
  const [loading, setLoading] = useState(false);
  return <LoadingContext.Provider value={{ loading, setLoading }}>{children}</LoadingContext.Provider>;
};

export const useLoading = () => useContext(LoadingContext);
