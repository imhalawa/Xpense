import { Box, Grid, Typography } from "@mui/material";
import XpensesDataGrid from "../../components/DataGrid/DataGrid";
import { createTransactionFixture } from "../../fixtures";

const transactions = createTransactionFixture();

const Transactions = () => {
  return (
    <Grid container spacing={2} mt={2}>
      <Grid item xs={12}>
        <Typography variant="h4">Transaction</Typography>
      </Grid>
      <Grid item spacing={2} xs={12}>
        <Box sx={{ height: "80vh", width: "100%" }}>
          <XpensesDataGrid
            rows={transactions}
            dense
            onClick={() => console.log("row clicked")}
          />
        </Box>
      </Grid>
    </Grid>
  );
};

export default Transactions;
