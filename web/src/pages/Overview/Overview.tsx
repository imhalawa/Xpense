import { useEffect, useState } from "react";
import { useLocation, useNavigate } from "react-router";
import dayjs from "dayjs";
import { Caption1, Title3, makeStyles, tokens } from "@fluentui/react-components";
import AccountBalances from "../../components/AccountBalances/AccountBalances";
import BudgetMeter from "../../components/BudgetMeter/BudgetMeter";
import { listAccounts } from "../../clients/options";
import { listBudgets } from "../../clients/budgets";
import { IAccountResponse, IBudgetResponse } from "../../clients/types";
import { useVault } from "../../vault/VaultProvider";
import { useTransactionFilter } from "../../transactions/useTransactionFilter";
import TransactionsView from "../Transactions/TransactionsView";

const fallbackSpace = "personal";

const useStyles = makeStyles({
  sections: {
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalXXXL,
  },
  section: {
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalM,
  },
  budgets: {
    display: "grid",
    gap: tokens.spacingHorizontalL,
    gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))",
  },
  empty: {
    color: tokens.colorNeutralForeground2,
  },
});

const Overview = () => {
  const styles = useStyles();
  const location = useLocation();
  const navigate = useNavigate();
  const { projection } = useVault();
  const transactionFilter = useTransactionFilter(projection, fallbackSpace);
  const [accounts, setAccounts] = useState<IAccountResponse[]>([]);
  const [budgets, setBudgets] = useState<IBudgetResponse[]>([]);
  const now = dayjs();

  useEffect(() => {
    listAccounts().then(setAccounts);
    listBudgets(dayjs()).then(setBudgets);
  }, []);

  return (
    <div className={styles.sections}>
      <section className={styles.section} aria-labelledby="balances-heading">
        <Title3 as="h2" id="balances-heading">
          Balances
        </Title3>
        <AccountBalances accounts={accounts} />
      </section>

      <section className={styles.section} aria-labelledby="budgets-heading">
        <Title3 as="h2" id="budgets-heading">
          Budgets
        </Title3>
        {budgets.length === 0 ? (
          <Caption1 className={styles.empty}>No budgets yet.</Caption1>
        ) : (
          <div className={styles.budgets}>
            {budgets.map((budget) => (
              <BudgetMeter key={budget.id} budget={budget} now={now} />
            ))}
          </div>
        )}
      </section>

      <section className={styles.section} aria-labelledby="recent-transactions-heading">
        <Title3 as="h2" id="recent-transactions-heading">
          Recent transactions
        </Title3>
        <TransactionsView
          filter={transactionFilter.filter}
          activeFilterCount={transactionFilter.activeFilterCount}
          limit={5}
          hidePagination
          onAddTransaction={() =>
            navigate("/transactions/new", {
              state: { returnTo: `${location.pathname}${location.search}` },
            })
          }
          onClearFilters={transactionFilter.clearFilters}
        />
      </section>
    </div>
  );
};

export default Overview;
