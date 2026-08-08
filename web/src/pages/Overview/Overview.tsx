import { useEffect, useState } from "react";
import dayjs from "dayjs";
import { Caption1, Title3, makeStyles, tokens } from "@fluentui/react-components";
import AccountBalances from "../../components/AccountBalances/AccountBalances";
import BudgetMeter from "../../components/BudgetMeter/BudgetMeter";
import { listAccounts } from "../../clients/options";
import { listBudgets } from "../../clients/budgets";
import { IAccountResponse, IBudgetResponse } from "../../clients/types";

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
    </div>
  );
};

export default Overview;
