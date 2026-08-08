import { Caption2 } from "@fluentui/react-components";
import { Badge } from "@fluentui/react-badge";
import { IAccountResponse } from "../../clients/types";
import { formatMoney } from "../../money/formatMoney";
import StatTile from "../StatTile/StatTile";

interface AccountBalancesProps {
  accounts: IAccountResponse[];
}

const AccountBalances = ({ accounts }: AccountBalancesProps) => {
  if (accounts.length === 0) {
    return <Caption2>No accounts yet.</Caption2>;
  }

  return (
    <div
      style={{
        display: "grid",
        gap: 16,
        gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))",
      }}>
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
