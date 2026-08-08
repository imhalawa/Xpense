import type { RefObject } from "react";
import { useLocation, useNavigate } from "react-router";
import {
  Dialog,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
} from "@fluentui/react-components";
import TransactionsForm from "../pages/Transactions/TransactionsForm/TransactionsForm";
import { useVault } from "../vault/VaultProvider";
import type { AccountView, TransactionDraft } from "../vault/VaultProjection";

interface TransactionRouteState {
  returnTo?: string;
}

interface TransactionDialogProps {
  accounts: AccountView[];
  activeSpace: string;
  returnFocusRef: RefObject<HTMLButtonElement | null>;
}

const TransactionDialog = ({
  accounts,
  activeSpace,
  returnFocusRef,
}: TransactionDialogProps) => {
  const location = useLocation();
  const navigate = useNavigate();
  const { projection } = useVault();
  const open = location.pathname === "/transactions/new";

  const close = () => {
    const routeState = location.state as TransactionRouteState | null;
    if (routeState?.returnTo === undefined) navigate("/transactions", { replace: true });
    else navigate(-1);
    window.setTimeout(() => returnFocusRef.current?.focus(), 0);
  };

  const save = async (draft: TransactionDraft) => {
    await projection.saveTransaction(draft);
    close();
  };

  return (
    <Dialog
      open={open}
      onOpenChange={(_event, data) => {
        if (!data.open) close();
      }}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Add transaction</DialogTitle>
          <DialogContent>
            {open && (
              <TransactionsForm
                accounts={accounts}
                activeSpace={activeSpace}
                onCancel={close}
                onSubmit={save}
              />
            )}
          </DialogContent>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
};

export default TransactionDialog;
