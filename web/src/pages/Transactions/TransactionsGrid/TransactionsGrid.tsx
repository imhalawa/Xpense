import {
  Badge,
  Body1,
  Button,
  Caption1,
  DataGrid,
  DataGridBody,
  DataGridCell,
  DataGridHeader,
  DataGridHeaderCell,
  DataGridRow,
  Link,
  MessageBar,
  MessageBarBody,
  TableColumnDefinition,
  createTableColumn,
  makeStyles,
  mergeClasses,
  tokens,
} from "@fluentui/react-components";
import {
  CalendarRegular,
  ChevronLeftRegular,
  ChevronRightRegular,
  FolderRegular,
  MoneyRegular,
  PaymentRegular,
  BuildingShopRegular,
  TagRegular,
} from "@fluentui/react-icons";
import { formatIsoDate } from "../../../utils/DateUtils";
import { toSingle } from "../../../typings/models/IMoney";
import { ITransactionResponse } from "../../../clients/types";
import { useLoading } from "../../../contexts/LoadingContext";
import { useCallback, useEffect, useMemo, useState } from "react";
import { listTransactions } from "../../../clients/transactions";
import { listCategories } from "../../../clients/options";
import { useTransctionUtilities } from "../../../contexts/TransactionUtilitiesContext";
import { Dayjs } from "dayjs";
import { ICategory } from "../../../typings/models/ICategory";

interface ITransactionsGridProps {
  size?: number;
  dense?: boolean;
  day?: Dayjs | null;
  from?: Dayjs | null;
  to?: Dayjs | null;
  hidePagination?: boolean;
}

const useStyles = makeStyles({
  surface: {
    borderRadius: tokens.borderRadiusLarge,
    backgroundColor: tokens.colorNeutralBackground1,
    boxShadow: tokens.shadow4,
    borderTop: `${tokens.strokeWidthThick} solid ${tokens.colorBrandStroke1}`,
  },
  amount: {
    display: "block",
    width: "100%",
    textAlign: "right",
    fontVariantNumeric: "tabular-nums",
  },
  desktopGrid: {
    overflowX: "auto",
    "@media (max-width: 767px)": {
      display: "none",
    },
  },
  mobileList: {
    display: "none",
    "@media (max-width: 767px)": {
      display: "flex",
      flexDirection: "column",
    },
  },
  mobileItem: {
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalM,
    padding: tokens.spacingHorizontalM,
    borderBottomWidth: tokens.strokeWidthThin,
    borderBottomStyle: "solid",
    borderBottomColor: tokens.colorNeutralStroke2,
    ":last-child": {
      borderBottomWidth: "0",
    },
  },
  mobileHeader: {
    display: "flex",
    alignItems: "baseline",
    justifyContent: "space-between",
    gap: tokens.spacingHorizontalM,
  },
  mobileAmount: {
    fontWeight: tokens.fontWeightSemibold,
    fontVariantNumeric: "tabular-nums",
  },
  mobileDetails: {
    display: "grid",
    gridTemplateColumns: "88px minmax(0, 1fr)",
    alignItems: "center",
    rowGap: tokens.spacingVerticalS,
    columnGap: tokens.spacingHorizontalM,
  },
  mobileLabel: {
    color: tokens.colorNeutralForeground2,
  },
  income: {
    color: "var(--xpense-income)",
  },
  expense: {
    color: "var(--xpense-expense)",
  },
  category0: {
    boxShadow: "inset 3px 0 0 var(--xpense-category-0)",
  },
  category1: {
    boxShadow: "inset 3px 0 0 var(--xpense-category-1)",
  },
  category2: {
    boxShadow: "inset 3px 0 0 var(--xpense-category-2)",
  },
  category3: {
    boxShadow: "inset 3px 0 0 var(--xpense-category-3)",
  },
  category4: {
    boxShadow: "inset 3px 0 0 var(--xpense-category-4)",
  },
  category5: {
    boxShadow: "inset 3px 0 0 var(--xpense-category-5)",
  },
  category6: {
    boxShadow: "inset 3px 0 0 var(--xpense-category-6)",
  },
  category7: {
    boxShadow: "inset 3px 0 0 var(--xpense-category-7)",
  },
  header: {
    display: "inline-flex",
    alignItems: "center",
    gap: tokens.spacingHorizontalXS,
  },
  tags: {
    display: "flex",
    flexWrap: "wrap",
    gap: tokens.spacingHorizontalXS,
  },
  empty: {
    margin: tokens.spacingHorizontalM,
  },
  pagination: {
    display: "flex",
    alignItems: "center",
    justifyContent: "flex-end",
    gap: tokens.spacingHorizontalS,
    padding: tokens.spacingHorizontalM,
  },
});

const TransactionsGrid = ({
  size,
  hidePagination,
  dense,
  day,
  from,
  to,
}: ITransactionsGridProps) => {
  const styles = useStyles();
  const { setLoading } = useLoading();
  const [transactions, setTransactions] = useState<ITransactionResponse[]>([]);
  const [categories, setCategories] = useState<Map<number, ICategory>>(new Map());
  const [pageSize, setPageSize] = useState<number>(size ?? 10);
  const [page, setPage] = useState<number>(1);
  const [pages, setPages] = useState<number>(0);
  const { submittedTransaction, setSubmittedTransaction } = useTransctionUtilities();
  const categoryTones = [
    styles.category0,
    styles.category1,
    styles.category2,
    styles.category3,
    styles.category4,
    styles.category5,
    styles.category6,
    styles.category7,
  ];

  useEffect(() => {
    listCategories()
      .then((all) => setCategories(new Map(all.map((category) => [category.id, category]))))
      .catch((loadError) => console.error(loadError));
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
      .catch((loadError) => {
        console.error(loadError);
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

  const columns = useMemo<TableColumnDefinition<ITransactionResponse>[]>(
    () => [
      createTableColumn({
        columnId: "amount",
        renderHeaderCell: () => (
          <span className={styles.header}>
            <MoneyRegular /> Amount
          </span>
        ),
        renderCell: (item) => (
          <Body1
            className={mergeClasses(
              styles.amount,
              item.kind === "income"
                ? styles.income
                : item.kind === "expense"
                  ? styles.expense
                  : undefined
            )}>
            {toSingle(item.amount)} {item.amount.currency}
          </Body1>
        ),
      }),
      createTableColumn({
        columnId: "date",
        renderHeaderCell: () => (
          <span className={styles.header}>
            <CalendarRegular /> Date
          </span>
        ),
        renderCell: (item) => formatIsoDate(item.occurredAt),
      }),
      createTableColumn({
        columnId: "category",
        renderHeaderCell: () => (
          <span className={styles.header}>
            <FolderRegular /> Category
          </span>
        ),
        renderCell: (item) => {
          const category = item.categoryId === null ? undefined : categories.get(item.categoryId);
          return category === undefined ? (
            "—"
          ) : (
            <Badge
              appearance="outline"
              className={
                categoryTones[Math.abs(item.categoryId ?? 0) % categoryTones.length]
              }>
              {category.label}
            </Badge>
          );
        },
      }),
      createTableColumn({
        columnId: "merchant",
        renderHeaderCell: () => (
          <span className={styles.header}>
            <BuildingShopRegular /> Merchant
          </span>
        ),
        renderCell: (item) => item.merchant?.label ?? "—",
      }),
      createTableColumn({
        columnId: "account",
        renderHeaderCell: () => (
          <span className={styles.header}>
            <PaymentRegular /> Account
          </span>
        ),
        renderCell: (item) => item.sourceAccountNumber ?? item.destinationAccountNumber ?? "—",
      }),
      createTableColumn({
        columnId: "tags",
        renderHeaderCell: () => (
          <span className={styles.header}>
            <TagRegular /> Tags
          </span>
        ),
        renderCell: (item) => (
          <span className={styles.tags}>
            {item.tags.map((tag) => (
              <Link key={tag.id} as="span">
                #{tag.label}
              </Link>
            ))}
          </span>
        ),
      }),
    ],
    [categories, styles]
  );

  return (
    <div className={styles.surface}>
      {transactions.length === 0 ? (
        <MessageBar className={styles.empty} intent="info">
          <MessageBarBody>No transactions found</MessageBarBody>
        </MessageBar>
      ) : (
        <>
          <div className={styles.desktopGrid}>
            <DataGrid
              items={transactions}
              columns={columns}
              size={dense ? "small" : "medium"}
              getRowId={(item) => item.id}>
              <DataGridHeader>
                <DataGridRow>
                  {({ renderHeaderCell }) => (
                    <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>
                  )}
                </DataGridRow>
              </DataGridHeader>
              <DataGridBody<ITransactionResponse>>
                {({ item, rowId }) => (
                  <DataGridRow<ITransactionResponse> key={rowId}>
                    {({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}
                  </DataGridRow>
                )}
              </DataGridBody>
            </DataGrid>
          </div>
          <div className={styles.mobileList}>
            {transactions.map((item) => {
              const category =
                item.categoryId === null ? undefined : categories.get(item.categoryId);
              const amountTone =
                item.kind === "income"
                  ? styles.income
                  : item.kind === "expense"
                    ? styles.expense
                    : undefined;

              return (
                <article
                  key={item.id}
                  className={styles.mobileItem}
                  aria-label={`${toSingle(item.amount)} ${item.amount.currency} on ${formatIsoDate(item.occurredAt)}`}>
                  <div className={styles.mobileHeader}>
                    <Body1 className={mergeClasses(styles.mobileAmount, amountTone)}>
                      {toSingle(item.amount)} {item.amount.currency}
                    </Body1>
                    <Caption1>{formatIsoDate(item.occurredAt)}</Caption1>
                  </div>
                  <div className={styles.mobileDetails}>
                    <Caption1 className={styles.mobileLabel}>Category</Caption1>
                    <span>
                      {category === undefined ? (
                        "—"
                      ) : (
                        <Badge
                          appearance="outline"
                          className={
                            categoryTones[
                              Math.abs(item.categoryId ?? 0) % categoryTones.length
                            ]
                          }>
                          {category.label}
                        </Badge>
                      )}
                    </span>
                    <Caption1 className={styles.mobileLabel}>Merchant</Caption1>
                    <span>{item.merchant?.label ?? "—"}</span>
                    <Caption1 className={styles.mobileLabel}>Account</Caption1>
                    <span>
                      {item.sourceAccountNumber ?? item.destinationAccountNumber ?? "—"}
                    </span>
                    <Caption1 className={styles.mobileLabel}>Tags</Caption1>
                    <span className={styles.tags}>
                      {item.tags.length === 0
                        ? "—"
                        : item.tags.map((tag) => (
                            <Link key={tag.id} as="span">
                              #{tag.label}
                            </Link>
                          ))}
                    </span>
                  </div>
                </article>
              );
            })}
          </div>
        </>
      )}

      {!hidePagination && pages > 1 && (
        <div className={styles.pagination}>
          <Button
            appearance="subtle"
            icon={<ChevronLeftRegular />}
            aria-label="Previous page"
            disabled={page <= 1}
            onClick={() => setPage((current) => current - 1)}
          />
          <Body1>
            Page {page} of {pages}
          </Body1>
          <Button
            appearance="subtle"
            icon={<ChevronRightRegular />}
            aria-label="Next page"
            disabled={page >= pages}
            onClick={() => setPage((current) => current + 1)}
          />
        </div>
      )}
    </div>
  );
};

export default TransactionsGrid;
