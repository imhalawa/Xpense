import { useEffect, useMemo, useState } from "react";
import type { KeyboardEvent, UIEvent } from "react";
import {
  Body1,
  Button,
  Caption1,
  DataGrid,
  DataGridBody,
  DataGridCell,
  DataGridHeader,
  DataGridHeaderCell,
  DataGridRow,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Menu,
  MenuItem,
  MenuList,
  MenuPopover,
  MenuTrigger,
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
import {
  ChevronLeftRegular,
  ChevronRightRegular,
  MoreHorizontalRegular,
} from "@fluentui/react-icons";
import CategoryChip from "../../components/Chips/CategoryChip/CategoryChip";
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
  canAddTransaction?: boolean;
  limit?: number;
  hidePagination?: boolean;
  refreshKey?: string | number;
  onEditTransaction?: (transaction: TransactionView) => void;
  onTransactionChanged?: () => void;
}

const useStyles = makeStyles({
  root: {
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalS,
  },
  card: {
    overflow: "hidden",
    backgroundColor: tokens.colorNeutralBackground1,
    borderRadius: tokens.borderRadiusLarge,
    borderTopWidth: tokens.strokeWidthThin,
    borderRightWidth: tokens.strokeWidthThin,
    borderBottomWidth: tokens.strokeWidthThin,
    borderLeftWidth: tokens.strokeWidthThin,
    borderTopStyle: "solid",
    borderRightStyle: "solid",
    borderBottomStyle: "solid",
    borderLeftStyle: "solid",
    borderTopColor: tokens.colorNeutralStroke2,
    borderRightColor: tokens.colorNeutralStroke2,
    borderBottomColor: tokens.colorNeutralStroke2,
    borderLeftColor: tokens.colorNeutralStroke2,
    boxShadow: tokens.shadow4,
  },
  surface: {
    overflow: "hidden",
  },
  footer: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    minHeight: "44px",
    paddingInline: tokens.spacingHorizontalM,
    borderTopWidth: tokens.strokeWidthThin,
    borderTopStyle: "solid",
    borderTopColor: tokens.colorNeutralStroke2,
  },
  amount: {
    display: "block",
    width: "100%",
    textAlign: "right",
    fontVariantNumeric: "tabular-nums",
  },
  amountCell: {
    display: "flex",
    alignItems: "center",
    gap: tokens.spacingHorizontalXS,
    ":hover button": {
      opacity: 1,
    },
    ":focus-within button": {
      opacity: 1,
    },
  },
  actionMenu: {
    flexShrink: 0,
    opacity: 0,
  },
  mobileActionMenu: {
    opacity: 1,
    minWidth: "44px",
    minHeight: "44px",
  },
  income: {
    color: "var(--xpense-income)",
  },
  expense: {
    color: "var(--xpense-expense)",
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
  interactiveRow: {
    cursor: "pointer",
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
  canAddTransaction = true,
  limit,
  hidePagination = false,
  refreshKey,
  onEditTransaction,
  onTransactionChanged,
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
  const [transactionPendingDeletion, setTransactionPendingDeletion] = useState<TransactionView | null>(null);
  const [deleteFailure, setDeleteFailure] = useState<string | null>(null);

  useEffect(() => {
    if (state !== "unlocked") return;

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
          return (
            <CategoryChip
              key={tagId}
              categoryId={tagId}
              foregroundHex={tag?.foregroundHex ?? null}
              backgroundHex={tag?.backgroundHex ?? null}
              label={tags.get(tagId) ?? tagId}
            />
          );
        })}
      </span>
    );

  const actionMenu = (transaction: TransactionView, isMobile = false) => {
    if (!transaction.canEdit || onEditTransaction === undefined) return null;

    return (
      <Menu>
        <MenuTrigger disableButtonEnhancement>
          <Button
            className={mergeClasses(styles.actionMenu, isMobile && styles.mobileActionMenu)}
            appearance="subtle"
            icon={<MoreHorizontalRegular />}
            aria-label={`Actions for transaction ${transaction.id}`}
            onClick={(event) => event.stopPropagation()}
          />
        </MenuTrigger>
        <MenuPopover>
          <MenuList>
            <MenuItem
              onClick={(event) => {
                event.stopPropagation();
                onEditTransaction(transaction);
              }}>
              Edit
            </MenuItem>
            <MenuItem
              onClick={(event) => {
                event.stopPropagation();
                setDeleteFailure(null);
                setTransactionPendingDeletion(transaction);
              }}>
              Delete
            </MenuItem>
          </MenuList>
        </MenuPopover>
      </Menu>
    );
  };

  const activateTransaction = (transaction: TransactionView) => {
    if (transaction.canEdit) onEditTransaction?.(transaction);
  };

  const rowKeyDown = (event: KeyboardEvent<HTMLElement>, transaction: TransactionView) => {
    if (!transaction.canEdit || (event.key !== "Enter" && event.key !== " ")) return;
    if ((event.target as HTMLElement).closest("button")) return;
    event.preventDefault();
    activateTransaction(transaction);
  };

  const confirmDelete = async () => {
    if (transactionPendingDeletion === null) return;

    try {
      await projection.deleteTransaction(filter.space, transactionPendingDeletion.id);
      setTransactions((rows) => rows.filter((row) => row.id !== transactionPendingDeletion.id));
      setTotalRows((count) => Math.max(count - 1, 0));
      setTransactionPendingDeletion(null);
      onTransactionChanged?.();
    } catch {
      setDeleteFailure("The transaction could not be deleted. Try again.");
    }
  };

  const columns = useMemo<TableColumnDefinition<TransactionView>[]>(
    () => [
      createTableColumn({
        columnId: "amount",
        renderHeaderCell: () => "Amount",
        renderCell: (item) => (
          <div className={styles.amountCell}>
            <Body1
              className={mergeClasses(
                styles.amount,
                item.kind === "income" ? styles.income : item.kind === "expense" ? styles.expense : undefined,
              )}>
              {formatAmount(item)}
            </Body1>
            {actionMenu(item)}
          </div>
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
            <CategoryChip
              categoryId={item.categoryId}
              label={categories.get(item.categoryId) ?? item.categoryId}
            />
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
    [accountLabels, categories, merchants, onEditTransaction, styles, tags, taxonomyById],
  );

  if (state === "locked") {
    return (
      <div className={styles.state}>
        <Body1>The vault is locked.</Body1>
      </div>
    );
  }

  if ((state === "unlocking" || isLoadingRows) && transactions.length === 0) {
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
        {canAddTransaction ? (
          <>
            <Body1>Add your first transaction to start your ledger.</Body1>
            <Button appearance="primary" onClick={onAddTransaction}>
              Add transaction
            </Button>
          </>
        ) : (
          <Body1>Create an account first, then record what moves through it.</Body1>
        )}
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

      <div className={styles.card}>
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
                <DataGridRow<TransactionView>
                  key={rowId}
                  className={item.canEdit ? styles.interactiveRow : undefined}
                  data-transaction-id={item.id}
                  tabIndex={item.canEdit ? 0 : undefined}
                  onClick={() => activateTransaction(item)}
                  onKeyDown={(event: KeyboardEvent<HTMLElement>) => rowKeyDown(event, item)}>
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
                aria-label={`${formatAmount(item)} on ${formatIsoDate(item.occurredAt)}`}
                data-transaction-id={item.id}
                tabIndex={item.canEdit ? 0 : undefined}
                onClick={() => activateTransaction(item)}
                onKeyDown={(event) => rowKeyDown(event, item)}>
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
                  {actionMenu(item, true)}
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

      <div className={styles.footer}>
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

      <Dialog
        open={transactionPendingDeletion !== null}
        onOpenChange={(_event, data) => {
          if (!data.open) setTransactionPendingDeletion(null);
        }}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Delete transaction?</DialogTitle>
            <DialogContent>
              <Body1>This permanently removes the transaction from this ledger.</Body1>
              {deleteFailure !== null && <MessageBar intent="error"><MessageBarBody>{deleteFailure}</MessageBarBody></MessageBar>}
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setTransactionPendingDeletion(null)}>
                Cancel
              </Button>
              <Button appearance="primary" onClick={() => void confirmDelete()}>
                Delete transaction
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );
};

export default TransactionsView;
