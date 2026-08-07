import { createContext, useContext, useState } from "react";
import { ITransactionResponse } from "../clients/types";

interface ITransactionUtilitiesContext {
  submittedTransaction: ITransactionResponse | null;
  setSubmittedTransaction: React.Dispatch<React.SetStateAction<ITransactionResponse | null>>;
}

const TranscationUtilitiesContext = createContext({} as ITransactionUtilitiesContext);

export const TransactionUtilitiesContextProvider = ({ children }: any) => {
  const [submittedTransaction, setSubmittedTransaction] = useState<ITransactionResponse | null>(null);

  return (
    <TranscationUtilitiesContext.Provider value={{ submittedTransaction, setSubmittedTransaction }}>
      {children}
    </TranscationUtilitiesContext.Provider>
  );
};

export const useTransctionUtilities = () => useContext(TranscationUtilitiesContext);
