import { createContext, useContext, useState } from "react";
import { ITransaction } from "../typings";

interface ITransactionUtilitiesContext {
  submittedTransaction: ITransaction | null;
  setSubmittedTransaction: React.Dispatch<React.SetStateAction<ITransaction | null>>;
}

const TranscationUtilitiesContext = createContext({} as ITransactionUtilitiesContext);

export const TransactionUtilitiesContextProvider = ({ children }: any) => {
  const [submittedTransaction, setSubmittedTransaction] = useState<ITransaction | null>(null);

  return (
    <TranscationUtilitiesContext.Provider value={{ submittedTransaction, setSubmittedTransaction }}>
      {children}
    </TranscationUtilitiesContext.Provider>
  );
};

export const useTransctionUtilities = () => useContext(TranscationUtilitiesContext);
