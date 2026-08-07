import Box from "@mui/material/Box";
import Chip from "@mui/material/Chip";
import Typography from "@mui/material/Typography";
import { IAccountResponse } from "../../clients/types";
import { formatMoney } from "../../money/formatMoney";
import StatTile from "../StatTile/StatTile";

interface AccountBalancesProps {
  accounts: IAccountResponse[];
}

const AccountBalances = ({ accounts }: AccountBalancesProps) => {
  if (accounts.length === 0)
    return (
      <Typography variant="body2" color="text.secondary">
        No accounts yet.
      </Typography>
    );

  return (
    <Box
      sx={{
        display: "grid",
        gap: 2,
        gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", lg: "repeat(4, 1fr)" },
      }}
    >
      {accounts.map((account) => (
        <StatTile
          key={account.accountNumber}
          label={account.label}
          value={formatMoney(account.balance)}
          hint={account.balance.currency}
          badge={account.isDefault ? <Chip size="small" label="Default" /> : undefined}
        />
      ))}
    </Box>
  );
};

export default AccountBalances;
