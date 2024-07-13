import { Box, Grid, Typography } from "@mui/material";
import TransactionsGrid from "../../components/TransactionsGrid/TransactionsGrid";

const Transactions = () => {
  return (
    <Grid container spacing={2} mt={2}>
      <Grid item xs={12}>
        <Typography variant="h4">Transaction</Typography>
      </Grid>
      <Grid item spacing={2} xs={12}>
        <Box sx={{ height: "80vh", width: "100%" }}>
          <TransactionsGrid />
        </Box>
      </Grid>
    </Grid>
  );
};

export default Transactions;
