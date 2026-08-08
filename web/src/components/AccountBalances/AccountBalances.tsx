import { Badge, Caption1, makeStyles, tokens } from "@fluentui/react-components";
import { IAccountResponse } from "../../clients/types";
import { formatMoney } from "../../money/formatMoney";
import StatTile from "../StatTile/StatTile";

interface AccountBalancesProps {
  accounts: IAccountResponse[];
}

const useStyles = makeStyles({
  grid: {
    display: "grid",
    gap: tokens.spacingHorizontalL,
    gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))",
  },
  empty: {
    color: tokens.colorNeutralForeground2,
  },
});

const AccountBalances = ({ accounts }: AccountBalancesProps) => {
  const styles = useStyles();

  if (accounts.length === 0) {
    return <Caption1 className={styles.empty}>No accounts yet.</Caption1>;
  }

  return (
    <div className={styles.grid}>
      {accounts.map((account) => (
        <StatTile
          key={account.accountNumber}
          label={account.label}
          value={formatMoney(account.balance)}
          hint={account.balance.currency}
          badge={account.isDefault ? <Badge appearance="filled">Default</Badge> : undefined}
        />
      ))}
    </div>
  );
};

export default AccountBalances;
