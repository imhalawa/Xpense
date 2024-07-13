import { Link, Typography } from "@mui/material";
import { Euro, DollarSign } from "lucide-react";
import CategoryChip from "../Chips/CategoryChip/CategoryChip";
import DataGrid, { IDataGridHeader } from "../DataGrid/DataGrid";
import { ITransactionRow } from "../../typings/ITransactionRow";
import { TransactionType } from "../../typings/ITransactionType";
import { formatDate } from "../../utils/DateUtils";
import { Currency } from "../../typings/IMoney";
import { createTransactionFixture } from "../../fixtures";

const headers: IDataGridHeader<ITransactionRow>[] = [
  {
    headerName: "ID",
    field: "id",
    order: 1,
  },
  {
    headerName: "Amount",
    field: "amount",
    order: 2,
    render: (row: ITransactionRow) => {
      return (
        <>
          {row.type == TransactionType.CREDIT ? (
            <Typography variant="body2" color="green">
              {row.amount.currency == Currency.EUR ? (
                <Euro size={12} />
              ) : (
                <DollarSign size={12} />
              )}
              {row.amount.value}
            </Typography>
          ) : (
            <Typography variant="body2" color="red">
              {row.amount.currency == Currency.EUR ? (
                <Euro size={12} />
              ) : (
                <DollarSign size={12} />
              )}
              {row.amount.value}
            </Typography>
          )}
        </>
      );
    },
  },
  {
    headerName: "Date",
    field: "date",
    order: 3,
    render: (row: ITransactionRow) => {
      return <>{formatDate(row.date)}</>;
    },
  },
  {
    headerName: "Category",
    field: "category",
    order: 4,
    render: (row: ITransactionRow) => {
      return (
        <CategoryChip
          id={row.category.id}
          name={row.category.name}
          priority={row.category.priority}
        />
      );
    },
  },
  {
    headerName: "Merchant",
    field: "merchant",
    order: 5,
    render: (row: ITransactionRow) => {
      return <>{row.merchant.name}</>;
    },
  },
  {
    headerName: "Account Number",
    field: "account",
    order: 6,
    render: (row: ITransactionRow) => {
      return <>{row.account.accountNumber}</>;
    },
  },
  {
    headerName: "Tags",
    field: "tags",
    order: 7,
    render: (row: ITransactionRow) => {
      return (
        <>
          {row.tags &&
            row.tags.map((tag) => (
              <Link
                underline="hover"
                sx={{
                  mr: 1,
                  "&:hover": {
                    cursor: "pointer",
                  },
                }}
                color="darkblue"
              >
                #{tag.name}
              </Link>
            ))}
        </>
      );
    },
  },
];

const TransactionsGrid = () => {
  const transactions = createTransactionFixture();
  return <DataGrid headers={headers} rows={transactions} dense />;
};

export default TransactionsGrid;
