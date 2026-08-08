import { useEffect, useState } from "react";
import dayjs from "dayjs";
import Box from "@mui/material/Box";
import Grid from "@mui/material/Grid";
import Typography from "@mui/material/Typography";
import AccountBalances from "../../components/AccountBalances/AccountBalances";
import BudgetMeter from "../../components/BudgetMeter/BudgetMeter";
import { listAccounts } from "../../clients/options";
import { listBudgets } from "../../clients/budgets";
import { IAccountResponse, IBudgetResponse } from "../../clients/types";
import PageHeader from "../../shell/PageHeader";

const Overview = () => {
  const [accounts, setAccounts] = useState<IAccountResponse[]>([]);
  const [budgets, setBudgets] = useState<IBudgetResponse[]>([]);
  const now = dayjs();

  useEffect(() => {
    listAccounts().then(setAccounts);
    listBudgets(dayjs()).then(setBudgets);
  }, []);

  return (
    <>
      <PageHeader title="Overview" />
      <Grid size={12}>
        <Box sx={{ display: "flex", flexDirection: "column", gap: 3, width: "100%" }}>
          <Box>
            <Typography variant="h2" sx={{ marginBottom: 1.5 }}>
              Balances
            </Typography>
            <AccountBalances accounts={accounts} />
          </Box>

          <Box>
            <Typography variant="h2" sx={{ marginBottom: 1.5 }}>
              Budgets
            </Typography>
            {budgets.length === 0 ? (
              <Typography variant="body2" color="text.secondary">
                No budgets yet.
              </Typography>
            ) : (
              <Box
                sx={{
                  display: "grid",
                  gap: 2,
                  gridTemplateColumns: { xs: "1fr", md: "repeat(2, 1fr)" },
                }}>
                {budgets.map((budget) => (
                  <BudgetMeter key={budget.id} budget={budget} now={now} />
                ))}
              </Box>
            )}
          </Box>
        </Box>
      </Grid>
    </>
  );
};

export default Overview;
