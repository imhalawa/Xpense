import { useEffect, useState } from "react";
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
import { useVault } from "../../vault/VaultProvider";
import type { AccountView } from "../../vault/VaultProjection";
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
  const transactionFilter = useTransactionFilter(projection, fallbackSpace);
  const [accounts, setAccounts] = useState<AccountView[]>([]);

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
  }, [projection, state, transactionFilter.filter.space]);

  const addTransaction = () =>
    navigate("/transactions/new", {
      state: { returnTo: `${location.pathname}${location.search}` },
    });

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
        onAddTransaction={addTransaction}
        onClearFilters={transactionFilter.clearFilters}
      />
    </div>
  );
};

export default Transactions;
