import { useEffect, useState, type RefObject } from "react";
import { useLocation, useNavigate } from "react-router";
import {
  Dialog,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Body1,
} from "@fluentui/react-components";
import TransactionsForm from "../pages/Transactions/TransactionsForm/TransactionsForm";
import { useVault } from "../vault/VaultProvider";
import type {
  AccountView,
  TaxonomyValue,
  TransactionDraft,
  TransactionView,
} from "../vault/VaultProjection";

interface TransactionRouteState {
  returnTo?: string;
  returnFocusId?: string;
}

const editRoute = /^\/transactions\/([^/]+)\/edit$/;

interface TransactionDialogProps {
  accounts: AccountView[];
  accountsLoaded: boolean;
  activeSpace: string;
  returnFocusRef: RefObject<HTMLButtonElement | null>;
}

const TransactionDialog = ({
  accounts,
  accountsLoaded,
  activeSpace,
  returnFocusRef,
}: TransactionDialogProps) => {
  const location = useLocation();
  const navigate = useNavigate();
  const { projection, state } = useVault();
  const editMatch = location.pathname.match(editRoute);
  const transactionId = editMatch === null ? null : decodeURIComponent(editMatch[1]);
  const routeRequestsDialog = location.pathname === "/transactions/new" || transactionId !== null;
  const open = routeRequestsDialog && state === "unlocked";
  const [transaction, setTransaction] = useState<TransactionView | null | undefined>(undefined);
  const [taxonomy, setTaxonomy] = useState<TaxonomyValue[]>([]);
  const [isLoaded, setIsLoaded] = useState(false);
  const [loadFailure, setLoadFailure] = useState<string | null>(null);

  useEffect(() => {
    if (!open) {
      setTransaction(undefined);
      setTaxonomy([]);
      setIsLoaded(false);
      setLoadFailure(null);
      return;
    }

    let isCurrent = true;
    setTransaction(undefined);
    setIsLoaded(false);
    setLoadFailure(null);
    const transactionRequest =
      transactionId === null
        ? Promise.resolve<TransactionView | null>(null)
        : projection.getTransaction(activeSpace, transactionId);
    Promise.all([
      transactionRequest,
      projection.listTaxonomy(activeSpace, "category"),
      projection.listTaxonomy(activeSpace, "merchant"),
      projection.listTaxonomy(activeSpace, "tag"),
    ])
      .then(([loadedTransaction, categories, merchants, tags]) => {
        if (!isCurrent) return;
        setTransaction(transactionId === null ? undefined : loadedTransaction);
        setTaxonomy([...categories, ...merchants, ...tags]);
        setIsLoaded(true);
      })
      .catch(() => {
        if (!isCurrent) return;
        setTransaction(null);
        setTaxonomy([]);
        setIsLoaded(true);
        setLoadFailure("The transaction form could not be loaded. Try again.");
      });

    return () => {
      isCurrent = false;
    };
  }, [activeSpace, open, projection, transactionId]);

  const close = (changed = false) => {
    const routeState = location.state as TransactionRouteState | null;
    const returnTo = routeState?.returnTo ?? "/transactions";
    navigate(returnTo, {
      replace: true,
      state:
        routeState?.returnFocusId === undefined
          ? changed
            ? { transactionChanged: true }
            : undefined
          : { returnFocusId: routeState.returnFocusId, transactionChanged: changed },
    });
    if (routeState?.returnFocusId === undefined) {
      window.setTimeout(() => returnFocusRef.current?.focus(), 0);
    }
  };

  const save = async (draft: TransactionDraft) => {
    await projection.saveTransaction(draft);
    close(true);
  };

  return (
    <Dialog
      open={open}
      onOpenChange={(_event, data) => {
        if (!data.open) close();
      }}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>{transactionId === null ? "Add transaction" : "Edit transaction"}</DialogTitle>
          <DialogContent>
            {(!isLoaded || !accountsLoaded) && <Body1>Loading transaction…</Body1>}
            {loadFailure !== null && <Body1>{loadFailure}</Body1>}
            {transactionId !== null && transaction === null && (
              <Body1>This transaction is no longer available.</Body1>
            )}
            {transactionId !== null && transaction !== null && transaction?.canEdit === false && (
              <Body1>This transaction cannot be edited.</Body1>
            )}
            {open && isLoaded && accountsLoaded && loadFailure === null && (
              transactionId === null ||
              (transaction !== undefined && transaction !== null && transaction.canEdit)
            ) && (
              <TransactionsForm
                accounts={accounts}
                activeSpace={activeSpace}
                transaction={transaction ?? undefined}
                taxonomy={taxonomy}
                onCreateCategory={(label, priority) =>
                  projection.createCategory(activeSpace, label, priority)
                }
                onCancel={close}
                onSubmit={save}
                submitLabel={transactionId === null ? "Create" : "Save"}
              />
            )}
          </DialogContent>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
};

export default TransactionDialog;
