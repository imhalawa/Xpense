import { Alert, Link, Typography } from "@mui/material";
import {
  AccountIcon,
  AmountIcon,
  CategoryIcon,
  DateIcon,
  DollarIcon,
  EuroIcon,
  MerchantIcon,
  TagsIcon,
} from "../../../icons/icons";
import CategoryChip from "../../../components/Chips/CategoryChip/CategoryChip";
import DataGrid, { IDataGridHeader } from "../../../components/DataGrid/DataGrid";
import { formatIsoDate } from "../../../utils/DateUtils";
import { Currency } from "../../../typings/enums/Currency";
import { ICategory } from "../../../typings/models/ICategory";
import { toSingle } from "../../../typings/models/IMoney";
import { ITransactionResponse } from "../../../clients/types";
import { useLoading } from "../../../contexts/LoadingContext";
import { useCallback, useEffect, useState } from "react";
import { listTransactions } from "../../../clients/transactions";
import { listCategories } from "../../../clients/options";
import { useTransctionUtilities } from "../../../contexts/TransactionUtilitiesContext";
import { Dayjs } from "dayjs";

interface ITransactionsGridProps {
  size?: number;
  dense?: boolean;
  day?: Dayjs | null;
  from?: Dayjs | null;
  to?: Dayjs | null;
  hidePagination?: boolean;
}

const buildHeaders = (
  categories: Map<number, ICategory>
): IDataGridHeader<ITransactionResponse>[] => [
  {
    headerName: "Amount",
    field: "amount",
    icon: <AmountIcon />,
    order: 2,
    render: (row: ITransactionResponse) => (
      <Typography variant="body2" color={row.kind === "income" ? "success.main" : "error.main"}>
        {row.amount.currency === Currency.EUR ? <EuroIcon size={12} /> : <DollarIcon size={12} />}
        {toSingle(row.amount)}
      </Typography>
    ),
  },
  {
    headerName: "Date",
    field: "occurredAt",
    order: 3,
    icon: <DateIcon />,
    render: (row: ITransactionResponse) => <>{formatIsoDate(row.occurredAt)}</>,
  },
  {
    headerName: "Category",
    field: "categoryId",
    order: 4,
    icon: <CategoryIcon />,
    render: (row: ITransactionResponse) => {
      const category = row.categoryId === null ? undefined : categories.get(row.categoryId);
      if (category === undefined) return <>&mdash;</>;
      return <CategoryChip id={category.id!} name={category.label} priority={category.priority} />;
    },
  },
  {
    headerName: "Merchant",
    field: "merchant",
    order: 5,
    icon: <MerchantIcon />,
    render: (row: ITransactionResponse) => <>{row.merchant?.label ?? "—"}</>,
  },
  {
    headerName: "Account Number",
    field: "sourceAccountNumber",
    order: 6,
    icon: <AccountIcon />,
    render: (row: ITransactionResponse) => (
      <>{row.sourceAccountNumber ?? row.destinationAccountNumber}</>
    ),
  },
  {
    headerName: "Tags",
    field: "tags",
    order: 7,
    icon: <TagsIcon />,
    render: (row: ITransactionResponse) => (
      <>
        {row.tags.map((tag) => (
          <Link
            key={tag.id}
            underline="hover"
            sx={{
              mr: 1,
              "&:hover": {
                cursor: "pointer",
              },
            }}
            color="primary"
          >
            #{tag.label}
          </Link>
        ))}
      </>
    ),
  },
];

const TransactionsGrid = ({ size, hidePagination, dense, day, from, to }: ITransactionsGridProps) => {
  const { setLoading } = useLoading();
  const [transactions, setTransactions] = useState<ITransactionResponse[]>([]);
  const [categories, setCategories] = useState<Map<number, ICategory>>(new Map());
  const [pageSize, setPageSize] = useState<number>(size ?? 10);
  const [page, setPage] = useState<number>(1);
  const [pages, setPages] = useState<number>(0);

  const { submittedTransaction, setSubmittedTransaction } = useTransctionUtilities();

  useEffect(() => {
    listCategories()
      .then((all) => setCategories(new Map(all.map((category) => [category.id, category]))))
      .catch((error) => console.error(error));
  }, []);

  const loadTransactions = useCallback(() => {
    setLoading(true);

    listTransactions({ page, pageSize, day, from, to })
      .then((response) => {
        setTransactions(response.items);
        setPageSize(response.pageSize);
        setPage(response.page);
        setPages(response.totalPages);
        setLoading(false);
      })
      .catch((error) => {
        console.error(error);
        setLoading(false);
      });
  }, [page, pageSize, day, from, to]);

  useEffect(() => {
    loadTransactions();
  }, [loadTransactions]);

  useEffect(() => {
    if (submittedTransaction === null) return;
    setSubmittedTransaction(null);
    loadTransactions();
  }, [submittedTransaction]);

  const onPageChange = (page: number) => {
    setPage(page);
  };

  return (
    <DataGrid
      headers={buildHeaders(categories)}
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
