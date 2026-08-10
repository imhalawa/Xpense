import { useEffect, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router";
import {
  MessageBar,
  MessageBarActions,
  MessageBarBody,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { DismissRegular } from "@fluentui/react-icons";
import { Button } from "@fluentui/react-components";
import { useTransactionFilter } from "../../transactions/useTransactionFilter";
import { useResourceVersion } from "../../contexts/ResourceVersionContext";
import { useVault } from "../../vault/VaultProvider";
import type { AccountView, TransactionView } from "../../vault/VaultProjection";
import TransactionsToolbar from "./TransactionsToolbar";
import TransactionsView from "./TransactionsView";

const fallbackSpace = "personal";

const useStyles = makeStyles({
  root: {
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalM,
  },
});

const Transactions = () => {
  const styles = useStyles();
  const location = useLocation();
  const navigate = useNavigate();
  const { projection, state } = useVault();
  const resourceVersion = useResourceVersion();
  const transactionFilter = useTransactionFilter(projection, fallbackSpace);
  const [accounts, setAccounts] = useState<AccountView[]>([]);
  const [refreshVersion, setRefreshVersion] = useState(0);
  const restoredFocusLocation = useRef<string | null>(null);

  useEffect(() => {
    if (state !== "unlocked") {
      setAccounts([]);
      return;
    }

    let isCurrent = true;
    projection
      .listAccounts(transactionFilter.filter.space)
      .then((loadedAccounts) => {
        if (isCurrent) setAccounts(loadedAccounts);
      })
      .catch(() => {
        if (isCurrent) setAccounts([]);
      });

    return () => {
      isCurrent = false;
    };
  }, [projection, resourceVersion, state, transactionFilter.filter.space]);

  const addTransaction = () =>
    navigate("/transactions/new", {
      state: { returnTo: `${location.pathname}${location.search}` },
    });

  const editTransaction = (transaction: TransactionView) => {
    navigate(`/transactions/${encodeURIComponent(transaction.id)}/edit`, {
      state: {
        returnTo: `${location.pathname}${location.search}`,
        returnFocusId: transaction.id,
      },
    });
  };

  useEffect(() => {
    const returnFocusId = (location.state as { returnFocusId?: string } | null)?.returnFocusId;
    if (returnFocusId === undefined || restoredFocusLocation.current === location.key) return;

    const row = document.querySelector<HTMLElement>(`[data-transaction-id="${returnFocusId}"]`);
    if (row === null) return;
    restoredFocusLocation.current = location.key;
    row.focus();
  }, [location.key, location.state, refreshVersion]);

  return (
    <div className={styles.root}>
      {transactionFilter.removedMessage !== null && (
        <MessageBar intent="warning">
          <MessageBarBody>{transactionFilter.removedMessage}</MessageBarBody>
          <MessageBarActions
            containerAction={
              <Button
                appearance="transparent"
                icon={<DismissRegular />}
                aria-label="Dismiss filter message"
                onClick={transactionFilter.dismissRemovedMessage}
              />
            }
          />
        </MessageBar>
      )}

      <TransactionsToolbar
        filter={transactionFilter.filter}
        accounts={accounts}
        onFilterChange={transactionFilter.setFilter}
        onClearFilters={transactionFilter.clearFilters}
      />
      <TransactionsView
        filter={transactionFilter.filter}
        activeFilterCount={transactionFilter.activeFilterCount}
        canAddTransaction={accounts.some((account) => account.canEdit)}
        refreshKey={`${location.key}-${refreshVersion}`}
        onAddTransaction={addTransaction}
        onClearFilters={transactionFilter.clearFilters}
        onEditTransaction={editTransaction}
        onTransactionChanged={() => setRefreshVersion((version) => version + 1)}
      />
    </div>
  );
};

export default Transactions;
