import {
  Badge,
  Dropdown,
  Field,
  Option,
  Popover,
  PopoverSurface,
  PopoverTrigger,
  Toolbar,
  ToolbarButton,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { DatePicker } from "@fluentui/react-datepicker-compat";
import dayjs from "dayjs";
import { countActiveFilters } from "../../transactions/transactionFilterState";
import type { AccountView, TransactionFilter } from "../../vault/VaultProjection";

interface TransactionsToolbarProps {
  filter: TransactionFilter;
  accounts: AccountView[];
  onFilterChange: (filter: TransactionFilter) => void;
  onClearFilters: () => void;
}

const useStyles = makeStyles({
  root: {
    display: "flex",
    alignItems: "center",
    gap: tokens.spacingHorizontalS,
    overflowX: "auto",
    paddingBlock: tokens.spacingVerticalXS,
  },
  account: {
    minWidth: "160px",
    maxWidth: "220px",
  },
  dates: {
    display: "grid",
    gridTemplateColumns: "repeat(2, minmax(180px, 1fr))",
    gap: tokens.spacingHorizontalM,
    padding: tokens.spacingHorizontalM,
    "@media (max-width: 479px)": {
      gridTemplateColumns: "1fr",
    },
  },
});

const dateSummary = (filter: TransactionFilter): string => {
  if (filter.from !== null && filter.to !== null) return `${filter.from} – ${filter.to}`;
  if (filter.from !== null) return `From ${filter.from}`;
  if (filter.to !== null) return `Until ${filter.to}`;
  return "All dates";
};

const calendarDate = (date: Date | null | undefined): string | null =>
  date == null ? null : dayjs(date).format("YYYY-MM-DD");

const TransactionsToolbar = ({
  filter,
  accounts,
  onFilterChange,
  onClearFilters,
}: TransactionsToolbarProps) => {
  const styles = useStyles();
  const activeFilterCount = countActiveFilters(filter);
  const selectedAccount = accounts.find((account) => account.id === filter.account);

  return (
    <Toolbar className={styles.root} aria-label="Transaction filters">
      <Popover positioning="below-start">
        <PopoverTrigger disableButtonEnhancement>
          <ToolbarButton>{dateSummary(filter)}</ToolbarButton>
        </PopoverTrigger>
        <PopoverSurface className={styles.dates}>
          <Field label="From">
            <DatePicker
              value={filter.from === null ? null : dayjs(filter.from).toDate()}
              formatDate={(date) => calendarDate(date) ?? ""}
              onSelectDate={(date) =>
                onFilterChange({ ...filter, from: calendarDate(date) })
              }
            />
          </Field>
          <Field label="To">
            <DatePicker
              value={filter.to === null ? null : dayjs(filter.to).toDate()}
              formatDate={(date) => calendarDate(date) ?? ""}
              onSelectDate={(date) => onFilterChange({ ...filter, to: calendarDate(date) })}
            />
          </Field>
        </PopoverSurface>
      </Popover>

      <Dropdown
        className={styles.account}
        aria-label="Account"
        value={selectedAccount?.label ?? "All accounts"}
        selectedOptions={filter.account === null ? [""] : [filter.account]}
        onOptionSelect={(_event, data) =>
          onFilterChange({ ...filter, account: data.optionValue === "" ? null : data.optionValue })
        }>
        <Option value="">All accounts</Option>
        {accounts.map((account) => (
          <Option key={account.id} value={account.id} text={account.label}>
            {account.label}
          </Option>
        ))}
      </Dropdown>

      {activeFilterCount > 0 && (
        <Badge appearance="tint" aria-label={`${activeFilterCount} active filters`}>
          {activeFilterCount}
        </Badge>
      )}

      <ToolbarButton disabled={activeFilterCount === 0} onClick={onClearFilters}>
        Clear filters
      </ToolbarButton>
    </Toolbar>
  );
};

export default TransactionsToolbar;
