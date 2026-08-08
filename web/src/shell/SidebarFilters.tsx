import { useMemo, useRef, useState } from "react";
import type { MouseEvent } from "react";
import {
  Body1,
  Button,
  Input,
  Popover,
  PopoverSurface,
  PopoverTrigger,
  makeStyles,
  mergeClasses,
  tokens,
} from "@fluentui/react-components";
import {
  BuildingShopRegular,
  CheckmarkRegular,
  ChevronDownRegular,
  ChevronRightRegular,
} from "@fluentui/react-icons";
import type {
  RecordId,
  TaxonomyKind,
  TaxonomyValue,
  TransactionFilter,
} from "../vault/VaultProjection";
import { serialiseTransactionFilter, toggleTaxonomyFilter } from "../transactions/transactionFilterState";
import { categoryPaletteSlot, resolveTagColors } from "../theme/tagColors";
import { orderTaxonomyValues, readTaxonomyRecency, rememberTaxonomyUse } from "./taxonomyOrder";

const visibleValueLimit = 5;
const sectionOrder: TaxonomyKind[] = ["category", "tag", "merchant"];
const sectionLabels: Record<TaxonomyKind, string> = {
  category: "Categories",
  tag: "Tags",
  merchant: "Merchants",
};

const useStyles = makeStyles({
  root: {
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalXS,
    marginTop: tokens.spacingVerticalM,
  },
  section: {
    display: "flex",
    flexDirection: "column",
  },
  sectionHeader: {
    display: "flex",
    alignItems: "center",
    gap: tokens.spacingHorizontalXS,
    width: "100%",
    minHeight: "32px",
    border: 0,
    borderRadius: tokens.borderRadiusMedium,
    paddingInline: tokens.spacingHorizontalS,
    color: tokens.colorNeutralForeground2,
    backgroundColor: "transparent",
    cursor: "pointer",
    textAlign: "start",
    ":hover": {
      backgroundColor: tokens.colorSubtleBackgroundHover,
    },
  },
  panel: {
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalXXS,
    paddingBlock: tokens.spacingVerticalXXS,
  },
  valueLink: {
    display: "flex",
    alignItems: "center",
    gap: tokens.spacingHorizontalS,
    minHeight: "32px",
    borderInlineStart: `${tokens.strokeWidthThick} solid transparent`,
    borderRadius: tokens.borderRadiusMedium,
    paddingInline: tokens.spacingHorizontalS,
    color: tokens.colorNeutralForeground2,
    textDecorationLine: "none",
    ":hover": {
      color: tokens.colorNeutralForeground1,
      backgroundColor: tokens.colorSubtleBackgroundHover,
    },
  },
  selectedValue: {
    borderInlineStartColor: tokens.colorBrandStroke1,
    color: tokens.colorBrandForeground1,
    backgroundColor: tokens.colorBrandBackground2,
  },
  marker: {
    flexShrink: 0,
    width: "16px",
    height: "16px",
    color: tokens.colorNeutralForeground3,
  },
  valueLabel: {
    flexGrow: 1,
    minWidth: 0,
    overflow: "hidden",
    textOverflow: "ellipsis",
    whiteSpace: "nowrap",
  },
  selectedIndicator: {
    display: "inline-flex",
    flexShrink: 0,
  },
  viewAll: {
    justifyContent: "flex-start",
    minHeight: "32px",
    paddingInline: tokens.spacingHorizontalL,
  },
  popover: {
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalS,
    width: "280px",
    maxHeight: "420px",
  },
  allValues: {
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalXXS,
    overflowY: "auto",
  },
});

interface TaxonomyMarkerProps {
  value: TaxonomyValue;
  className: string;
}

const TaxonomyMarker = ({ value, className }: TaxonomyMarkerProps) => {
  if (value.kind === "category") {
    const categoryColor = `var(--xpense-category-${categoryPaletteSlot(value.id)})`;
    return (
      <svg className={className} viewBox="0 0 16 16" aria-hidden>
        <circle cx="8" cy="8" r="5" fill={categoryColor} />
      </svg>
    );
  }

  if (value.kind === "tag") {
    const colors = resolveTagColors(value.foregroundHex, value.backgroundHex);
    return (
      <svg className={className} viewBox="0 0 16 16" aria-hidden>
        <circle cx="8" cy="8" r="5" fill={colors.background} stroke={colors.foreground} />
      </svg>
    );
  }

  return <BuildingShopRegular className={className} aria-hidden />;
};

interface SidebarFiltersProps {
  values: TaxonomyValue[];
  filter: TransactionFilter;
  onToggleTaxonomy: (kind: TaxonomyKind, id: RecordId) => void;
}

const SidebarFilters = ({ values, filter, onToggleTaxonomy }: SidebarFiltersProps) => {
  const styles = useStyles();
  const [expandedSection, setExpandedSection] = useState<TaxonomyKind | null>("category");
  const [viewAllSection, setViewAllSection] = useState<TaxonomyKind | null>(null);
  const [search, setSearch] = useState("");
  const [recencyVersion, setRecencyVersion] = useState(0);
  const viewAllTriggers = useRef<Partial<Record<TaxonomyKind, HTMLButtonElement | null>>>({});

  const orderedValues = useMemo(
    () =>
      Object.fromEntries(
        sectionOrder.map((kind) => [
          kind,
          orderTaxonomyValues(
            values.filter((value) => value.kind === kind),
            readTaxonomyRecency(kind),
          ),
        ]),
      ) as Record<TaxonomyKind, TaxonomyValue[]>,
    [values, recencyVersion],
  );

  const selectValue =
    (value: TaxonomyValue) => (event: MouseEvent<HTMLAnchorElement>) => {
      if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;

      event.preventDefault();
      rememberTaxonomyUse(value.kind, value.id);
      setRecencyVersion((version) => version + 1);
      onToggleTaxonomy(value.kind, value.id);
      setViewAllSection(null);
      setSearch("");
    };

  const renderValue = (value: TaxonomyValue) => {
    const isSelected = filter[value.kind] === value.id;
    const target = toggleTaxonomyFilter(filter, value.kind, value.id);
    const searchParameters = serialiseTransactionFilter(target).toString();

    return (
      <a
        key={value.id}
        href={`/transactions?${searchParameters}`}
        className={mergeClasses(styles.valueLink, isSelected && styles.selectedValue)}
        onClick={selectValue(value)}>
        <TaxonomyMarker value={value} className={styles.marker} />
        <Body1 className={styles.valueLabel}>{value.label}</Body1>
        {isSelected && (
          <span className={styles.selectedIndicator} role="img" aria-label="Selected">
            <CheckmarkRegular aria-hidden />
          </span>
        )}
      </a>
    );
  };

  return (
    <div className={styles.root} aria-label="Transaction filters">
      {sectionOrder.map((kind) => {
        const label = sectionLabels[kind];
        const lowerLabel = label.toLocaleLowerCase();
        const isExpanded = expandedSection === kind;
        const panelId = `sidebar-${kind}-filters`;
        const sectionValues = orderedValues[kind];
        const matchingValues = sectionValues.filter((value) =>
          value.label.toLocaleLowerCase().includes(search.toLocaleLowerCase()),
        );

        return (
          <div key={kind} className={styles.section}>
            <button
              type="button"
              className={styles.sectionHeader}
              aria-expanded={isExpanded}
              aria-controls={panelId}
              onClick={() => setExpandedSection(isExpanded ? null : kind)}>
              {isExpanded ? <ChevronDownRegular aria-hidden /> : <ChevronRightRegular aria-hidden />}
              <Body1>{label}</Body1>
            </button>

            {isExpanded && (
              <div id={panelId} className={styles.panel} role="region" aria-label={label}>
                {sectionValues.slice(0, visibleValueLimit).map(renderValue)}
                {sectionValues.length > visibleValueLimit && (
                  <Popover
                    open={viewAllSection === kind}
                    onOpenChange={(_event, data) => {
                      setViewAllSection(data.open ? kind : null);
                      if (!data.open) {
                        setSearch("");
                        queueMicrotask(() => viewAllTriggers.current[kind]?.focus());
                      }
                    }}>
                    <PopoverTrigger disableButtonEnhancement>
                      <Button
                        ref={(element) => {
                          viewAllTriggers.current[kind] = element;
                        }}
                        className={styles.viewAll}
                        appearance="subtle">
                        View all {lowerLabel}
                      </Button>
                    </PopoverTrigger>
                    <PopoverSurface className={styles.popover} aria-label={`All ${lowerLabel}`}>
                      <Input
                        aria-label={`Search ${lowerLabel}`}
                        value={search}
                        onChange={(_event, data) => setSearch(data.value)}
                      />
                      <div className={styles.allValues}>{matchingValues.map(renderValue)}</div>
                    </PopoverSurface>
                  </Popover>
                )}
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
};

export default SidebarFilters;
