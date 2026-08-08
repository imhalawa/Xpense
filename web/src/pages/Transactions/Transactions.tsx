import { useState } from "react";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Dialog from "@mui/material/Dialog";
import DialogContent from "@mui/material/DialogContent";
import DialogTitle from "@mui/material/DialogTitle";
import Grid from "@mui/material/Grid";
import { DatePicker } from "@mui/x-date-pickers";
import dayjs, { Dayjs } from "dayjs";
import Page from "../../components/Page/Page";
import TransactionsGrid from "./TransactionsGrid/TransactionsGrid";
import TransactionsForm from "./TransactionsForm/TransactionsForm";
import { DateIcon, PlusIcon } from "../../icons/icons";

const Transactions = () => {
  const [from, setFrom] = useState<Dayjs | null>(dayjs().startOf("month"));
  const [to, setTo] = useState<Dayjs | null>(dayjs().endOf("month"));
  const [isFormOpen, setIsFormOpen] = useState(false);

  return (
    <Page title="Transactions">
      <Grid size={12}>
        <Box sx={{ display: "flex", flexDirection: "column", gap: 2, width: "100%" }}>
          <Box
            sx={{
              display: "flex",
              flexWrap: "wrap",
              alignItems: "center",
              gap: 2,
            }}>
            <DatePicker
              label="From"
              value={from}
              slots={{ openPickerIcon: DateIcon }}
              slotProps={{ textField: { size: "small" } }}
              onChange={(picked) => setFrom(picked)}
            />
            <DatePicker
              label="To"
              value={to}
              slots={{ openPickerIcon: DateIcon }}
              slotProps={{ textField: { size: "small" } }}
              onChange={(picked) => setTo(picked)}
            />
            <Box sx={{ marginLeft: "auto" }}>
              <Button
                variant="contained"
                startIcon={<PlusIcon size={18} />}
                onClick={() => setIsFormOpen(true)}>
                New transaction
              </Button>
            </Box>
          </Box>

          <TransactionsGrid size={10} from={from} to={to} />
        </Box>
      </Grid>

      <Dialog
        open={isFormOpen}
        onClose={() => setIsFormOpen(false)}
        fullWidth
        maxWidth="sm">
        <DialogTitle>New transaction</DialogTitle>
        <DialogContent>
          {isFormOpen && (
            <TransactionsForm
              onCancel={() => setIsFormOpen(false)}
              onSubmitted={() => setIsFormOpen(false)}
            />
          )}
        </DialogContent>
      </Dialog>
    </Page>
  );
};

export default Transactions;
