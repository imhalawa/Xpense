import { Grid } from "@mui/material";
import Typography from "@mui/material/Typography";
import XpensePieChart from "../../components/Charts/XpensePie/XpensePieChart.tsx";
import Box from "@mui/material/Box";
import XpenseAreaChart, { XpenseAreaChartEntry } from "../../components/Charts/XpenseLineCharts/XpenseLineCharts.tsx";
import TransactionsGrid from "../Transactions/TransactionsGrid/TransactionsGrid.tsx";
import Page from "../../components/Page/Page.tsx";
import { useEffect, useState } from "react";
import axios from "axios";
import { IResponse } from "../../clients/types/IResponse.ts";
import { ITodayExpensesByCategory } from "../../typings/models/ITodayExpensesByCategory.ts";
import { PieValueType } from "@mui/x-charts";
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

const Dashboard = () => {
  // TODO: create a proper type
  const [todayExpensesByCategory, setTodayExpensesByCategory] = useState<{
    data: PieValueType[] | null;
    value: string | null;
  }>({ data: null, value: null });

  useEffect(() => {
    const getExpenses = async () => {
      return await axios
        .get<IResponse<ITodayExpensesByCategory>>("/api/analytics/today/categories")
        .then((response) => response.data);
    };

    getExpenses().then((data) => {
      var pieData = data.data.expenses.map(
        (e) => ({ id: e.id, value: toSingle(e.amount), label: e.category.label }) as PieValueType
      );
      setTodayExpensesByCategory({
        data: pieData,
        value: toSingle(data.data.total).toString(),
      });
    });
  }, []);

  return (
    <Page title="Dashboard" headerColor="primary.dark" headerBackgroundColor="white">
      <Grid item lg={3} md={12} xs={12}>
        <Box display="flex" flexDirection={"column"} alignItems="center" justifyItems={"center"} >
          <XpensePieChart data={todayExpensesByCategory?.data} value={todayExpensesByCategory?.value} />
        </Box>
      </Grid>
      <Grid item lg={9} md={12} xs={12} my={2}>
        <TransactionsGrid size={6} dense hidePagination date={dayjs().unix()} />
      </Grid>
      <Grid item xs={12} sm={12} md={6} lg={4}>
        <Typography variant="h4" my={1}>
          {" "}
          Weekly Transactions
        </Typography>
        <XpenseAreaChart data={areaChartData} height={400} width={400} hideLegend={true} />
      </Grid>
      <Grid item xs={12} sm={12} md={6} lg={4}>
        <Typography variant="h4" my={1}>
          {" "}
          Monthly Overview
        </Typography>
        <XpenseAreaChart data={areaChartData} height={400} width={400} hideLegend={true} />
      </Grid>
      <Grid item xs={12} sm={12} md={6} lg={4}>
        <Typography variant="h4" my={1}>
          {" "}
          Yearly Overview
        </Typography>
        <XpenseAreaChart data={areaChartData} height={400} width={400} hideLegend={true} />
      </Grid>
    </Page>
  );
};

export default Dashboard;
