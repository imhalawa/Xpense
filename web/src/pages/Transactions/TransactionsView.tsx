import { useEffect, useMemo, useState } from "react";
import type { UIEvent } from "react";
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
  MessageBar,
  MessageBarBody,
  Skeleton,
  SkeletonItem,
  createTableColumn,
  makeStyles,
  mergeClasses,
  tokens,
} from "@fluentui/react-components";
import type { TableColumnDefinition } from "@fluentui/react-components";
import { ChevronLeftRegular, ChevronRightRegular } from "@fluentui/react-icons";
import { categoryPaletteSlot, resolveTagColors } from "../../theme/tagColors";
import { formatIsoDate } from "../../utils/DateUtils";
import { useVault } from "../../vault/VaultProvider";
import type {
  AccountView,
  TaxonomyValue,
  TransactionFilter,
  TransactionView,
} from "../../vault/VaultProjection";

const desktopQuery = "(min-width: 768px)";
const fetchLimit = 2000;
const localPageSize = 50;
const virtualiseAbove = 200;
const virtualRowHeight = 44;
const virtualViewportRows = 10;
const virtualOverscanRows = 5;

interface TransactionsViewProps {
  filter: TransactionFilter;
  activeFilterCount: number;
  onAddTransaction: () => void;
  onClearFilters: () => void;
  limit?: number;
  hidePagination?: boolean;
  refreshKey?: number;
}

const useStyles = makeStyles({
  root: {
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalS,
  },
  surface: {
    overflow: "hidden",
    borderTopWidth: tokens.strokeWidthThin,
    borderTopStyle: "solid",
    borderTopColor: tokens.colorNeutralStroke2,
    borderBottomWidth: tokens.strokeWidthThin,
    borderBottomStyle: "solid",
    borderBottomColor: tokens.colorNeutralStroke2,
  },
  amount: {
    display: "block",
    width: "100%",
    textAlign: "right",
    fontVariantNumeric: "tabular-nums",
  },
  income: {
    color: "var(--xpense-income)",
  },
  expense: {
    color: "var(--xpense-expense)",
  },
  category: {
    borderInlineStartWidth: tokens.strokeWidthThick,
    borderInlineStartStyle: "solid",
  },
  tags: {
    display: "flex",
    flexWrap: "wrap",
    gap: tokens.spacingHorizontalXS,
  },
  mobileList: {
    display: "flex",
    flexDirection: "column",
  },
  mobileItem: {
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalM,
    minHeight: "44px",
    paddingBlock: tokens.spacingVerticalM,
    paddingInline: tokens.spacingHorizontalM,
    borderBottomWidth: tokens.strokeWidthThin,
    borderBottomStyle: "solid",
    borderBottomColor: tokens.colorNeutralStroke2,
  },
  mobileHeader: {
    display: "flex",
    alignItems: "baseline",
    justifyContent: "space-between",
    gap: tokens.spacingHorizontalM,
  },
  mobileDetails: {
    display: "grid",
    gridTemplateColumns: "88px minmax(0, 1fr)",
    alignItems: "center",
    rowGap: tokens.spacingVerticalS,
    columnGap: tokens.spacingHorizontalM,
  },
  secondary: {
    color: tokens.colorNeutralForeground2,
  },
  state: {
    display: "flex",
    flexDirection: "column",
    alignItems: "flex-start",
    gap: tokens.spacingVerticalM,
    paddingBlock: tokens.spacingVerticalXXL,
  },
  skeleton: {
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalXS,
  },
  skeletonRow: {
    height: "44px",
  },
  pagination: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    minHeight: "44px",
  },
  paginationActions: {
    display: "flex",
    alignItems: "center",
    gap: tokens.spacingHorizontalXS,
  },
  paginationButton: {
    minWidth: "44px",
    minHeight: "44px",
  },
});

const useDesktopLayout = (): boolean => {
  const [matches, setMatches] = useState(() => window.matchMedia(desktopQuery).matches);

  useEffect(() => {
    const media = window.matchMedia(desktopQuery);
    const update = () => setMatches(media.matches);
    update();
    media.addEventListener("change", update);
    return () => media.removeEventListener("change", update);
  }, []);

  return matches;
};

const formatAmount = (transaction: TransactionView): string =>
  `${(transaction.amountMinorUnits / 100).toFixed(2)} ${transaction.currency}`;

const labelMap = (values: TaxonomyValue[], kind: TaxonomyValue["kind"]): Map<string, string> =>
  new Map(
    values.filter((value) => value.kind === kind).map((value) => [value.id, value.label]),
  );

const TransactionsView = ({
  filter,
  activeFilterCount,
  onAddTransaction,
  onClearFilters,
  limit,
  hidePagination = false,
  refreshKey,
}: TransactionsViewProps) => {
  const styles = useStyles();
  const isDesktop = useDesktopLayout();
  const { projection, state } = useVault();
  const [transactions, setTransactions] = useState<TransactionView[]>([]);
  const [totalRows, setTotalRows] = useState(0);
  const [accounts, setAccounts] = useState<AccountView[]>([]);
  const [taxonomy, setTaxonomy] = useState<TaxonomyValue[]>([]);
  const [isLoadingRows, setIsLoadingRows] = useState(true);
  const [syncFailure, setSyncFailure] = useState<string | null>(null);
  const [pageIndex, setPageIndex] = useState(0);
  const [scrollTop, setScrollTop] = useState(0);

  useEffect(() => {
    if (state !== "ready") return;

    let isCurrent = true;
    setIsLoadingRows(true);
    Promise.all([
      projection.queryTransactions(filter, { offset: 0, limit: fetchLimit }),
      projection.listAccounts(filter.space),
      projection.listTaxonomy(filter.space, "category"),
      projection.listTaxonomy(filter.space, "merchant"),
      projection.listTaxonomy(filter.space, "tag"),
    ])
      .then(([page, loadedAccounts, categories, merchants, tags]) => {
        if (!isCurrent) return;
        setTransactions(page.rows);
        setTotalRows(page.totalRows);
        setAccounts(loadedAccounts);
        setTaxonomy([...categories, ...merchants, ...tags]);
        setSyncFailure(null);
        setPageIndex(0);
        setScrollTop(0);
      })
      .catch(() => {
        if (isCurrent) setSyncFailure("Transactions could not be refreshed. Cached rows are shown.");
      })
      .finally(() => {
        if (isCurrent) setIsLoadingRows(false);
      });

    return () => {
      isCurrent = false;
    };
  }, [projection, state, filter, refreshKey]);

  const categories = useMemo(() => labelMap(taxonomy, "category"), [taxonomy]);
  const merchants = useMemo(() => labelMap(taxonomy, "merchant"), [taxonomy]);
  const tags = useMemo(() => labelMap(taxonomy, "tag"), [taxonomy]);
  const taxonomyById = useMemo(
    () => new Map(taxonomy.map((value) => [value.id, value])),
    [taxonomy],
  );
  const accountLabels = useMemo(
    () => new Map(accounts.map((account) => [account.id, account.label])),
    [accounts],
  );

  const pageSize = limit ?? localPageSize;
  const pageCount = Math.max(Math.ceil(transactions.length / pageSize), 1);
  const localRows = transactions.slice(pageIndex * pageSize, (pageIndex + 1) * pageSize);
  const shouldVirtualise = transactions.length > virtualiseAbove && limit === undefined;
  const virtualStart = shouldVirtualise
    ? Math.max(Math.floor(scrollTop / virtualRowHeight) - virtualOverscanRows, 0)
    : 0;
  const renderedRows = shouldVirtualise
    ? localRows.slice(virtualStart, virtualStart + virtualViewportRows + virtualOverscanRows * 2)
    : localRows;

  const accountLabel = (transaction: TransactionView): string => {
    if (transaction.isCounterpartyPrivate) return "Private account";
    if (transaction.accountId === null) return "—";
    return accountLabels.get(transaction.accountId) ?? transaction.accountId;
  };

  const tagBadges = (transaction: TransactionView) =>
    transaction.tagIds.length === 0 ? (
      "—"
    ) : (
      <span className={styles.tags}>
        {transaction.tagIds.map((tagId) => {
          const tag = taxonomyById.get(tagId);
          const colors = resolveTagColors(
            tag?.foregroundHex ?? null,
            tag?.backgroundHex ?? null,
          );
          return (
            <Badge
              key={tagId}
              appearance="filled"
              style={{ color: colors.foreground, backgroundColor: colors.background }}>
              {tags.get(tagId) ?? tagId}
            </Badge>
          );
        })}
      </span>
    );

  const columns = useMemo<TableColumnDefinition<TransactionView>[]>(
    () => [
      createTableColumn({
        columnId: "amount",
        renderHeaderCell: () => "Amount",
        renderCell: (item) => (
          <Body1
            className={mergeClasses(
              styles.amount,
              item.kind === "income" ? styles.income : item.kind === "expense" ? styles.expense : undefined,
            )}>
            {formatAmount(item)}
          </Body1>
        ),
      }),
      createTableColumn({
        columnId: "date",
        renderHeaderCell: () => "Date",
        renderCell: (item) => formatIsoDate(item.occurredAt),
      }),
      createTableColumn({
        columnId: "category",
        renderHeaderCell: () => "Category",
        renderCell: (item) =>
          item.categoryId === null ? (
            "—"
          ) : (
            <Badge
              appearance="outline"
              className={styles.category}
              style={{
                borderInlineStartColor: `var(--xpense-category-${categoryPaletteSlot(item.categoryId)})`,
              }}>
              {categories.get(item.categoryId) ?? item.categoryId}
            </Badge>
          ),
      }),
      createTableColumn({
        columnId: "merchant",
        renderHeaderCell: () => "Merchant",
        renderCell: (item) =>
          item.merchantId === null ? "—" : merchants.get(item.merchantId) ?? item.merchantId,
      }),
      createTableColumn({
        columnId: "account",
        renderHeaderCell: () => "Account",
        renderCell: accountLabel,
      }),
      createTableColumn({
        columnId: "tags",
        renderHeaderCell: () => "Tags",
        renderCell: tagBadges,
      }),
    ],
    [accountLabels, categories, merchants, styles, tags, taxonomyById],
  );

  if (state === "locked") {
    return (
      <div className={styles.state}>
        <Body1>The vault is locked.</Body1>
        <Button appearance="primary" onClick={() => void projection.unlock()}>
          Unlock
        </Button>
      </div>
    );
  }

  if ((state === "loading" || isLoadingRows) && transactions.length === 0) {
    return (
      <Skeleton className={styles.skeleton} aria-label="Loading transactions">
        {Array.from({ length: 5 }, (_value, index) => (
          <SkeletonItem key={index} className={styles.skeletonRow} />
        ))}
      </Skeleton>
    );
  }

  if (transactions.length === 0 && !isLoadingRows) {
    return activeFilterCount === 0 ? (
      <div className={styles.state}>
        <Body1>Add your first transaction to start your ledger.</Body1>
        <Button appearance="primary" onClick={onAddTransaction}>
          Add transaction
        </Button>
      </div>
    ) : (
      <div className={styles.state}>
        <Body1>No transactions match these filters.</Body1>
        <Button onClick={onClearFilters}>Clear filters</Button>
      </div>
    );
  }

  return (
    <div className={styles.root}>
      {syncFailure !== null && (
        <MessageBar role="status" intent="warning">
          <MessageBarBody>{syncFailure}</MessageBarBody>
        </MessageBar>
      )}

      <div
        className={styles.surface}
        onScroll={(event: UIEvent<HTMLDivElement>) => setScrollTop(event.currentTarget.scrollTop)}>
        {isDesktop ? (
          <DataGrid
            role="table"
            items={renderedRows}
            columns={columns}
            size="medium"
            getRowId={(item) => item.id}>
            <DataGridHeader>
              <DataGridRow>
                {({ renderHeaderCell }) => (
                  <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>
                )}
              </DataGridRow>
            </DataGridHeader>
            <DataGridBody<TransactionView>>
              {({ item, rowId }) => (
                <DataGridRow<TransactionView> key={rowId}>
                  {({ renderCell }) => (
                    <DataGridCell style={{ height: "44px" }}>{renderCell(item)}</DataGridCell>
                  )}
                </DataGridRow>
              )}
            </DataGridBody>
          </DataGrid>
        ) : (
          <div className={styles.mobileList} role="list" aria-label="Transactions">
            {renderedRows.map((item) => (
              <article
                key={item.id}
                role="listitem"
                className={styles.mobileItem}
                aria-label={`${formatAmount(item)} on ${formatIsoDate(item.occurredAt)}`}>
                <div className={styles.mobileHeader}>
                  <Body1
                    className={mergeClasses(
                      styles.amount,
                      item.kind === "income"
                        ? styles.income
                        : item.kind === "expense"
                          ? styles.expense
                          : undefined,
                    )}>
                    {formatAmount(item)}
                  </Body1>
                  <Caption1>{formatIsoDate(item.occurredAt)}</Caption1>
                </div>
                <div className={styles.mobileDetails}>
                  <Caption1 className={styles.secondary}>Category</Caption1>
                  <Body1>
                    {item.categoryId === null
                      ? "—"
                      : categories.get(item.categoryId) ?? item.categoryId}
                  </Body1>
                  <Caption1 className={styles.secondary}>Merchant</Caption1>
                  <Body1>
                    {item.merchantId === null
                      ? "—"
                      : merchants.get(item.merchantId) ?? item.merchantId}
                  </Body1>
                  <Caption1 className={styles.secondary}>Account</Caption1>
                  <Body1>{accountLabel(item)}</Body1>
                  <Caption1 className={styles.secondary}>Tags</Caption1>
                  <Body1>{tagBadges(item)}</Body1>
                </div>
              </article>
            ))}
          </div>
        )}
      </div>

      <div className={styles.pagination}>
        <Caption1 aria-label="Transaction total">{totalRows} transactions</Caption1>
        {!hidePagination && limit === undefined && pageCount > 1 && (
          <div className={styles.paginationActions}>
            <Button
              className={styles.paginationButton}
              appearance="subtle"
              icon={<ChevronLeftRegular />}
              aria-label="Previous page"
              disabled={pageIndex === 0}
              onClick={() => setPageIndex((current) => current - 1)}
            />
            <Body1>
              Page {pageIndex + 1} of {pageCount}
            </Body1>
            <Button
              className={styles.paginationButton}
              appearance="subtle"
              icon={<ChevronRightRegular />}
              aria-label="Next page"
              disabled={pageIndex >= pageCount - 1}
              onClick={() => setPageIndex((current) => current + 1)}
            />
          </div>
        )}
      </div>
    </div>
  );
};

export default TransactionsView;
