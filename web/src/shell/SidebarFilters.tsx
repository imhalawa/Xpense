import { useRef, useState } from "react";
import type { FocusEvent, KeyboardEvent, MouseEvent } from "react";
import {
  Body1,
  Button,
  Menu,
  MenuItem,
  MenuList,
  MenuPopover,
  MenuTrigger,
  makeStyles,
  mergeClasses,
  tokens,
} from "@fluentui/react-components";
import {
  AddRegular,
  BuildingShopRegular,
  CheckmarkRegular,
  ChevronDownRegular,
  ChevronRightRegular,
  DeleteRegular,
  EditRegular,
  MoreHorizontalRegular,
  WalletRegular,
} from "@fluentui/react-icons";
import type {
  AccountView,
  RecordId,
  TaxonomyKind,
  TaxonomyValue,
  TransactionFilter,
} from "../vault/VaultProjection";
import { serialiseTransactionFilter, toggleTaxonomyFilter } from "../transactions/transactionFilterState";
import { categoryPaletteSlot, resolveTagColors } from "../theme/tagColors";

export type SidebarResource =
  | { kind: "account"; value: AccountView }
  | { kind: TaxonomyKind; value: TaxonomyValue };

export type SidebarResourceAction = "create" | "edit" | "delete";

const taxonomyKinds: TaxonomyKind[] = ["category", "tag", "merchant"];
const sectionLabels: Record<SidebarResource["kind"], string> = {
  account: "Accounts",
  category: "Categories",
  tag: "Tags",
  merchant: "Merchants",
};
const singularLabels: Record<SidebarResource["kind"], string> = {
  account: "account",
  category: "category",
  tag: "tag",
  merchant: "merchant",
};

const useStyles = makeStyles({
  root: { display: "flex", flexDirection: "column", gap: tokens.spacingVerticalXS, marginTop: tokens.spacingVerticalM },
  section: { display: "flex", flexDirection: "column" },
  sectionHeader: { display: "flex", alignItems: "center", minHeight: "32px", paddingInline: tokens.spacingHorizontalS },
  sectionToggle: { flexGrow: 1, justifyContent: "flex-start", color: tokens.colorNeutralForeground2 },
  add: { minWidth: "32px" },
  panel: { display: "flex", flexDirection: "column", gap: tokens.spacingVerticalXXS, paddingBlock: tokens.spacingVerticalXXS },
  value: { display: "flex", alignItems: "center", gap: tokens.spacingHorizontalXS, minHeight: "32px", borderInlineStartWidth: tokens.strokeWidthThick, borderInlineStartStyle: "solid", borderInlineStartColor: "transparent", borderRadius: tokens.borderRadiusMedium, paddingInlineStart: tokens.spacingHorizontalS, ":hover": { backgroundColor: tokens.colorSubtleBackgroundHover }, ":focus-within": { backgroundColor: tokens.colorSubtleBackgroundHover } },
  valueLink: { display: "flex", alignItems: "center", gap: tokens.spacingHorizontalS, flexGrow: 1, minWidth: 0, color: tokens.colorNeutralForeground2, textDecorationLine: "none" },
  selectedValue: { borderInlineStartColor: tokens.colorBrandStroke1, backgroundColor: tokens.colorBrandBackground2, color: tokens.colorBrandForeground1 },
  marker: { flexShrink: 0, width: "16px", height: "16px", color: tokens.colorNeutralForeground3 },
  valueLabel: { flexGrow: 1, minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" },
  selectedIndicator: { display: "inline-flex", flexShrink: 0 },
  actions: { minWidth: "28px", visibility: "hidden" },
  actionVisible: { visibility: "visible" },
});

const TaxonomyMarker = ({ value, className }: { value: TaxonomyValue; className: string }) => {
  if (value.kind === "category") {
    return <svg className={className} viewBox="0 0 16 16" aria-hidden><circle cx="8" cy="8" r="5" fill={`var(--xpense-category-${categoryPaletteSlot(value.id)})`} /></svg>;
  }
  if (value.kind === "tag") {
    const colors = resolveTagColors(value.foregroundHex, value.backgroundHex);
    return <svg className={className} viewBox="0 0 16 16" aria-hidden><circle cx="8" cy="8" r="5" fill={colors.background} stroke={colors.foreground} /></svg>;
  }
  return <BuildingShopRegular className={className} aria-hidden />;
};

interface SidebarFiltersProps {
  accounts: AccountView[];
  values: TaxonomyValue[];
  filter: TransactionFilter;
  canEdit: boolean;
  onToggleTaxonomy: (kind: TaxonomyKind, id: RecordId) => void;
  onToggleAccount: (id: RecordId) => void;
  onResourceAction: (action: SidebarResourceAction, resource: SidebarResource, trigger: HTMLElement) => void;
}

const SidebarFilters = ({ accounts, values, filter, canEdit, onToggleTaxonomy, onToggleAccount, onResourceAction }: SidebarFiltersProps) => {
  const styles = useStyles();
  const [expandedSections, setExpandedSections] = useState<Record<SidebarResource["kind"], boolean>>({
    account: false,
    category: true,
    tag: false,
    merchant: false,
  });
  const [openMenu, setOpenMenu] = useState<string | null>(null);
  const [activeRow, setActiveRow] = useState<string | null>(null);
  const actionTriggers = useRef<Record<string, HTMLElement | null>>({});
  const sectionTriggers = useRef<Partial<Record<SidebarResource["kind"], HTMLElement | null>>>({});
  const isCoarsePointer = window.matchMedia?.("(hover: none), (pointer: coarse)").matches === true;

  const toggle = (kind: SidebarResource["kind"]) => setExpandedSections((expanded) => ({
    ...expanded,
    [kind]: !expanded[kind],
  }));
  const resourcesFor = (kind: SidebarResource["kind"]): SidebarResource[] =>
    kind === "account" ? accounts.map((value) => ({ kind, value })) : values.filter((value) => value.kind === kind).map((value) => ({ kind, value }));

  const newResource = (kind: SidebarResource["kind"]): SidebarResource =>
    kind === "account"
      ? { kind, value: { id: "", label: "", currency: "EUR" as AccountView["currency"], canEdit: true } }
      : { kind, value: { id: "", kind, label: "", foregroundHex: null, backgroundHex: null } };

  const select = (resource: SidebarResource) => (event: MouseEvent<HTMLAnchorElement>) => {
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    if (resource.kind === "account") onToggleAccount(resource.value.id);
    else onToggleTaxonomy(resource.kind, resource.value.id);
  };

  const hrefFor = (resource: SidebarResource): string => {
    const next = resource.kind === "account"
      ? { ...filter, account: filter.account === resource.value.id ? null : resource.value.id }
      : toggleTaxonomyFilter(filter, resource.kind, resource.value.id);
    return `/transactions?${serialiseTransactionFilter(next).toString()}`;
  };

  const isSelected = (resource: SidebarResource): boolean =>
    resource.kind === "account" ? filter.account === resource.value.id : filter[resource.kind] === resource.value.id;

  const keyboardMenu = (resource: SidebarResource) => (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.shiftKey && event.key === "F10") {
      event.preventDefault();
      setOpenMenu(`${resource.kind}-${resource.value.id}`);
    }
  };

  const leaveRow = (key: string) => (event: FocusEvent<HTMLDivElement>) => {
    if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
      setActiveRow((active) => active === key ? null : active);
    }
  };

  const renderResource = (resource: SidebarResource) => {
    const key = `${resource.kind}-${resource.value.id}`;
    const selected = isSelected(resource);
    const editable = canEdit && resource.value.canEdit !== false;
    const actionTrigger = () => actionTriggers.current[key];
    const startAction = (action: "edit" | "delete") => {
      const trigger = action === "delete" ? sectionTriggers.current[resource.kind] : actionTrigger();
      if (trigger !== null && trigger !== undefined) onResourceAction(action, resource, trigger);
    };
    return <div
      key={key}
      className={mergeClasses(styles.value, selected && styles.selectedValue)}
      onMouseEnter={() => setActiveRow(key)}
      onMouseLeave={() => { if (openMenu !== key) setActiveRow(null); }}
      onFocusCapture={() => setActiveRow(key)}
      onBlurCapture={leaveRow(key)}
      onContextMenu={(event) => { event.preventDefault(); if (editable) setOpenMenu(key); }}
      onKeyDown={keyboardMenu(resource)}>
      <a href={hrefFor(resource)} className={styles.valueLink} onClick={select(resource)}>
        {resource.kind === "account" ? <WalletRegular className={styles.marker} aria-hidden /> : <TaxonomyMarker value={resource.value} className={styles.marker} />}
        <Body1 className={styles.valueLabel}>{resource.value.label}</Body1>
        {selected && <span className={styles.selectedIndicator} role="img" aria-label="Selected"><CheckmarkRegular aria-hidden /></span>}
      </a>
      {editable && <Menu open={openMenu === key} onOpenChange={(_event, data) => setOpenMenu(data.open ? key : null)}>
        <MenuTrigger disableButtonEnhancement>
          <Button
            ref={(element) => { actionTriggers.current[key] = element; }}
            className={mergeClasses(styles.actions, (isCoarsePointer || activeRow === key || openMenu === key) && styles.actionVisible)}
            appearance="subtle"
            size="small"
            icon={<MoreHorizontalRegular />}
            aria-label={`Actions for ${resource.value.label}`}
          />
        </MenuTrigger>
        <MenuPopover><MenuList>
          <MenuItem icon={<EditRegular />} onClick={() => startAction("edit")}>Edit</MenuItem>
          <MenuItem icon={<DeleteRegular />} onClick={() => startAction("delete")}>Delete</MenuItem>
        </MenuList></MenuPopover>
      </Menu>}
    </div>;
  };

  const sections: SidebarResource["kind"][] = ["account", ...taxonomyKinds];
  return <div className={styles.root} aria-label="Transaction filters">
    {sections.map((kind) => {
      const isExpanded = expandedSections[kind];
      const label = sectionLabels[kind];
      return <div key={kind} className={styles.section}>
        <div className={styles.sectionHeader}>
          <Button ref={(element) => { sectionTriggers.current[kind] = element; }} className={styles.sectionToggle} appearance="subtle" icon={isExpanded ? <ChevronDownRegular /> : <ChevronRightRegular />} aria-expanded={isExpanded} aria-controls={`sidebar-${kind}`} onClick={() => toggle(kind)}>{label}</Button>
          {canEdit && <Button className={styles.add} appearance="subtle" size="small" icon={<AddRegular />} aria-label={`Add ${singularLabels[kind]}`} onClick={(event) => onResourceAction("create", newResource(kind), event.currentTarget)} />}
        </div>
        {isExpanded && <div id={`sidebar-${kind}`} className={styles.panel} role="region" aria-label={label}>{resourcesFor(kind).map(renderResource)}</div>}
      </div>;
    })}
  </div>;
};

export default SidebarFilters;
