import { Grid } from "@mui/material";
import Typography from "@mui/material/Typography";
import XpensePieChart from "../../components/Charts/XpensePie/XpensePieChart.tsx";
import Box from "@mui/material/Box";
import XpenseAreaChart, { XpenseAreaChartEntry } from "../../components/Charts/XpenseLineCharts/XpenseLineCharts.tsx";
import TransactionsGrid from "../Transactions/TransactionsGrid/TransactionsGrid.tsx";
import Page from "../../components/Page/Page.tsx";
import { useEffect, useState } from "react";
import { getSpendingByCategory } from "../../clients/analytics.ts";
import { PieValueType } from "@mui/x-charts";
import { IMoney } from "../../typings";
import { toSingle } from "../../typings/models/IMoney.ts";
import dayjs from "dayjs";

const areaChartData: XpenseAreaChartEntry[] = [
  {
    date: new Date("2024-07-1"),
    value: 1150,
  },
  {
    date: new Date("2024-07-2"),
    value: 20,
  },
  {
    date: new Date("2024-07-3"),
    value: 300,
  },
  {
    date: new Date("2024-07-4"),
    value: 1750,
  },
  {
    date: new Date("2024-07-5"),
    value: 300,
  },
  {
    date: new Date("2024-07-6"),
    value: 552,
  },
];

const format = (money: IMoney): string => `${toSingle(money)} ${money.currency}`;

const Dashboard = () => {
  // TODO: create a proper type
  const [todayExpensesByCategory, setTodayExpensesByCategory] = useState<{
    data: PieValueType[] | null;
    value: string | null;
  }>({ data: null, value: null });

  useEffect(() => {
    getSpendingByCategory().then((spending) => {
      const pieData = spending.expenses.map(
        (expense) =>
          ({
            id: `${expense.id}-${expense.amount.currency}`,
            value: toSingle(expense.amount),
            label: expense.category.label,
          }) as PieValueType
      );
      setTodayExpensesByCategory({
        data: pieData,
        value: spending.totals.map(format).join(" · ") || "0",
      });
    });
  }, []);

  return (
    <Page title="Dashboard" headerColor="primary.dark" headerBackgroundColor="white">
      <Grid
        size={{
          lg: 3,
          md: 12,
          xs: 12
        }}>
        <Box
          sx={{
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            justifyItems: "center"
          }}>
          <XpensePieChart data={todayExpensesByCategory?.data} value={todayExpensesByCategory?.value} />
        </Box>
      </Grid>
      <Grid
        size={{
          lg: 9,
          md: 12,
          xs: 12
        }}
        sx={{
          my: 2
        }}>
        <TransactionsGrid size={6} dense hidePagination day={dayjs()} />
      </Grid>
      <Grid
        size={{
          xs: 12,
          sm: 12,
          md: 6,
          lg: 4
        }}>
        <Typography variant="h4" sx={{
          my: 1
        }}>
          {" "}
          Weekly Transactions
        </Typography>
        <XpenseAreaChart data={areaChartData} height={400} width={400} hideLegend={true} />
      </Grid>
      <Grid
        size={{
          xs: 12,
          sm: 12,
          md: 6,
          lg: 4
        }}>
        <Typography variant="h4" sx={{
          my: 1
        }}>
          {" "}
          Monthly Overview
        </Typography>
        <XpenseAreaChart data={areaChartData} height={400} width={400} hideLegend={true} />
      </Grid>
      <Grid
        size={{
          xs: 12,
          sm: 12,
          md: 6,
          lg: 4
        }}>
        <Typography variant="h4" sx={{
          my: 1
        }}>
          {" "}
          Yearly Overview
        </Typography>
        <XpenseAreaChart data={areaChartData} height={400} width={400} hideLegend={true} />
      </Grid>
    </Page>
  );
};

export default Dashboard;
