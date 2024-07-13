import { Link, Typography } from "@mui/material";
import {
  Euro,
  DollarSign,
  CurrencyIcon,
  CalendarDaysIcon,
  CircleAlertIcon,
  StoreIcon,
  CreditCardIcon,
  TagIcon,
} from "lucide-react";
import CategoryChip from "../../../components/Chips/CategoryChip/CategoryChip";
import DataGrid, {
  IDataGridHeader,
} from "../../../components/DataGrid/DataGrid";
import { ITransaction } from "../../../typings/ITransaction";
import { TransactionType } from "../../../typings/ITransactionType";
import { formatDate } from "../../../utils/DateUtils";
import { Currency } from "../../../typings/IMoney";

const headers: IDataGridHeader<ITransaction>[] = [
  {
    headerName: "Amount",
    field: "amount",
    icon: <CurrencyIcon />,
    order: 2,
    render: (row: ITransaction) => {
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
    icon: <CalendarDaysIcon />,
    render: (row: ITransaction) => {
      return <>{formatDate(row.date)}</>;
    },
  },
  {
    headerName: "Category",
    field: "category",
    order: 4,
    icon: <CircleAlertIcon />,
    render: (row: ITransaction) => {
      return (
        <CategoryChip
          id={row.category.id}
          name={row.category.label}
          priority={row.category.priority}
        />
      );
    },
  },
  {
    headerName: "Merchant",
    field: "merchant",
    order: 5,
    icon: <StoreIcon />,
    render: (row: ITransaction) => {
      return <>{row.merchant.label}</>;
    },
  },
  {
    headerName: "Account Number",
    field: "account",
    order: 6,
    icon: <CreditCardIcon />,
    render: (row: ITransaction) => {
      return <>{row.account.accountNumber}</>;
    },
  },
  {
    headerName: "Tags",
    field: "tags",
    order: 7,
    icon: <TagIcon />,
    render: (row: ITransaction) => {
      return (
        <>
          {row.tags &&
            row.tags.map((tag, index) => (
              <Link
                key={index}
                underline="hover"
                sx={{
                  mr: 1,
                  "&:hover": {
                    cursor: "pointer",
                  },
                }}
                color="darkblue"
              >
                #{tag.label}
              </Link>
            ))}
        </>
      );
    },
  },
];

interface ITransactionsGridProps {
  transactions: ITransaction[];
}

const TransactionsGrid = ({ transactions }: ITransactionsGridProps) => {
  return (
    <DataGrid
      headers={headers}
      rows={transactions.sort((a, b) => b.date - a.date)}
    />
  );
};

export default TransactionsGrid;
