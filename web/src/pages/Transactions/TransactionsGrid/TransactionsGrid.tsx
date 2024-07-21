import { Alert, Link, Typography } from "@mui/material";
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
import DataGrid, { IDataGridHeader } from "../../../components/DataGrid/DataGrid";
import { formatDate } from "../../../utils/DateUtils";
import { Currency, ITransaction, TransactionType } from "../../../typings";
import { toSingle } from "../../../typings/models/IMoney";
import { useLoading } from "../../../contexts/LoadingContext";
import { useCalendar } from "../../../contexts/CalendarContext";
import { useEffect, useState } from "react";
import { filter } from "../../../clients/transactions";
import { useTransctionUtilities } from "../../../contexts/TransactionUtilitiesContext";

interface ITransactionsGridProps {
  size?: number;
  dense?: boolean;
  hidePagination?: boolean;
}

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
              {row.amount.currency == Currency.EUR ? <Euro size={12} /> : <DollarSign size={12} />}
              {toSingle(row.amount)}
            </Typography>
          ) : (
            <Typography variant="body2" color="red">
              {row.amount.currency == Currency.EUR ? <Euro size={12} /> : <DollarSign size={12} />}
              {toSingle(row.amount)}
            </Typography>
          )}
        </>
      );
    },
  },
  {
    headerName: "Date",
    field: "createdOn",
    order: 3,
    icon: <CalendarDaysIcon />,
    render: (row: ITransaction) => {
      return <>{formatDate(row.createdOn!)}</>;
    },
  },
  {
    headerName: "Category",
    field: "category",
    order: 4,
    icon: <CircleAlertIcon />,
    render: (row: ITransaction) => {
      return <CategoryChip id={row.category.id!} name={row.category.label} priority={row.category.priority} />;
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

const TransactionsGrid = ({ size, hidePagination, dense }: ITransactionsGridProps) => {
  const { setLoading } = useLoading();
  const { selectedDate } = useCalendar();
  const [transactions, setTransactions] = useState<ITransaction[]>([]);
  const [pageSize, setPageSize] = useState<number>(size ?? 10);
  const [page, setPage] = useState<number>(1);
  const [pages, setPages] = useState<number>(0);

  const { submittedTransaction, setSubmittedTransaction } = useTransctionUtilities();

  useEffect(() => {
    const fetchTransactions = async (page: number, size: number, date?: number) => {
      const response = await filter(page, size, date);
      setTransactions(response.data);
      setPageSize(response.size);
      setPage(response.page);
      setPages(response.pages);
    };

    console.log(selectedDate?.unix());

    setLoading(true);
    fetchTransactions(page, pageSize, selectedDate?.unix())
      .then(() => {
        console.log("Transactions fetched");
        setLoading(false);
      })
      .catch((error) => {
        console.error(error);
        setLoading(false);
      });
  }, [page, pageSize, selectedDate]);

  useEffect(() => {
    if (submittedTransaction != null) {
      setTransactions([...transactions, submittedTransaction]);
      setSubmittedTransaction(null);
    }
  }, [submittedTransaction]);

  const onPageChange = (page: number) => {
    setPage(page);
  };

  return (
    <DataGrid
      headers={headers}
      rows={transactions}
      paginated={!hidePagination && true}
      dense={dense ?? false}
      activePage={page}
      onPageChange={onPageChange}
      count={pages}
      emptyAlert={
        <Alert severity="info" sx={{ width: "100%" }}>
          No transactions found
        </Alert>
      }
    />
  );
};

export default TransactionsGrid;
