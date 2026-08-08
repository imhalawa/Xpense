import { useState } from "react";
import {
  Button,
  Dialog,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Field,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { DatePicker } from "@fluentui/react-datepicker-compat";
import { AddRegular } from "@fluentui/react-icons";
import dayjs, { Dayjs } from "dayjs";
import TransactionsGrid from "./TransactionsGrid/TransactionsGrid";
import TransactionsForm from "./TransactionsForm/TransactionsForm";
import PageHeader from "../../shell/PageHeader";

const useStyles = makeStyles({
  body: {
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalL,
  },
  filters: {
    display: "grid",
    gridTemplateColumns: "repeat(2, minmax(180px, 1fr))",
    gap: tokens.spacingHorizontalM,
    maxWidth: "480px",
    "@media (max-width: 479px)": {
      gridTemplateColumns: "1fr",
    },
  },
});

const Transactions = () => {
  const styles = useStyles();
  const [from, setFrom] = useState<Dayjs | null>(dayjs().startOf("month"));
  const [to, setTo] = useState<Dayjs | null>(dayjs().endOf("month"));
  const [isFormOpen, setIsFormOpen] = useState(false);

  return (
    <>
      <PageHeader
        title="Transactions"
        actions={
          <Button
            appearance="primary"
            icon={<AddRegular />}
            onClick={() => setIsFormOpen(true)}>
            New transaction
          </Button>
        }
      />

      <div className={styles.body}>
        <div className={styles.filters}>
          <Field label="From">
            <DatePicker
              value={from?.toDate() ?? null}
              formatDate={(date) =>
                date === undefined ? "" : dayjs(date).format("YYYY-MM-DD")
              }
              onSelectDate={(date) => setFrom(date == null ? null : dayjs(date))}
            />
          </Field>
          <Field label="To">
            <DatePicker
              value={to?.toDate() ?? null}
              formatDate={(date) =>
                date === undefined ? "" : dayjs(date).format("YYYY-MM-DD")
              }
              onSelectDate={(date) => setTo(date == null ? null : dayjs(date))}
            />
          </Field>
        </div>

        <TransactionsGrid size={10} from={from} to={to} />
      </div>

      <Dialog
        open={isFormOpen}
        onOpenChange={(_event, data) => setIsFormOpen(Boolean(data.open))}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>New transaction</DialogTitle>
            <DialogContent>
              {isFormOpen && (
                <TransactionsForm
                  onCancel={() => setIsFormOpen(false)}
                  onSubmitted={() => setIsFormOpen(false)}
                />
              )}
            </DialogContent>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </>
  );
};

export default Transactions;
