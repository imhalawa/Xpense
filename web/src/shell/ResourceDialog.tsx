import { useEffect, useState } from "react";
import {
  Button,
  Caption1,
  Checkbox,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Field,
  Input,
  MessageBar,
  MessageBarBody,
  Select,
  makeStyles,
  mergeClasses,
  tokens,
} from "@fluentui/react-components";
import { Currency } from "../typings/enums/Currency";
import { matchingPreset, tagPresetChoices } from "../theme/tagPresets";
import type { AccountDraft, AccountView, CategoryPriority, TaxonomyDraft } from "../vault/VaultProjection";
import type { SidebarResource, SidebarResourceAction } from "./SidebarFilters";

export interface ResourceDialogRequest {
  action: SidebarResourceAction;
  resource: SidebarResource;
  trigger: HTMLElement;
}

interface ResourceDialogProps {
  request: ResourceDialogRequest | null;
  accounts?: AccountView[];
  onClose: () => void;
  onSubmit: (request: ResourceDialogRequest, draft?: AccountDraft | TaxonomyDraft) => Promise<void>;
}

const useStyles = makeStyles({
  form: { display: "flex", flexDirection: "column", gap: tokens.spacingVerticalM },
  colors: { display: "flex", gap: tokens.spacingHorizontalM },
  swatches: { display: "flex", flexWrap: "wrap", gap: tokens.spacingHorizontalS },
  swatch: {
    display: "inline-flex",
    alignItems: "center",
    minWidth: "auto",
    borderRadius: tokens.borderRadiusCircular,
    borderTopStyle: "none",
    borderRightStyle: "none",
    borderBottomStyle: "none",
    borderLeftStyle: "none",
    paddingInline: tokens.spacingHorizontalM,
    paddingBlock: tokens.spacingVerticalXS,
    fontFamily: tokens.fontFamilyBase,
    fontSize: tokens.fontSizeBase200,
    fontWeight: tokens.fontWeightSemibold,
    outlineStyle: "solid",
    outlineWidth: tokens.strokeWidthThick,
    outlineColor: "transparent",
    outlineOffset: "2px",
    cursor: "pointer",
    transitionProperty: "outline-color, transform",
    transitionDuration: tokens.durationNormal,
    transitionTimingFunction: tokens.curveEasyEase,
    ":hover": { transform: "translateY(-1px)" },
  },
  swatchChosen: { outlineColor: tokens.colorBrandStroke1 },
  hint: { color: tokens.colorNeutralForeground3 },
});

const categoryPriorities: CategoryPriority[] = [
  "Essential",
  "Important",
  "Useful",
  "Optional",
  "Avoidable",
];

const initialDraft = (request: ResourceDialogRequest): AccountDraft | TaxonomyDraft => {
  if (request.resource.kind === "account") {
    const account = request.resource.value;
    return { label: account.label, currency: account.currency, openingBalanceMinorUnits: account.balanceMinorUnits ?? 0, isDefault: account.isDefault ?? false };
  }
  const value = request.resource.value;
  return { label: value.label, priority: value.priority ?? "Useful", foregroundHex: value.foregroundHex ?? "#242424", backgroundHex: value.backgroundHex ?? "#EDEDED" };
};

const resourceName = (resource: SidebarResource): string => resource.kind === "account" ? "account" : resource.kind;

const minorUnitsPerMajor = 100;

const toMajorUnits = (minorUnits: number): string =>
  (minorUnits / minorUnitsPerMajor).toFixed(2);

const parseMajorUnits = (value: string): number | null => {
  const trimmed = value.trim();
  if (trimmed === "" || !/^\d+([.,]\d{1,2})?$/u.test(trimmed)) return null;
  return Math.round(Number(trimmed.replace(",", ".")) * minorUnitsPerMajor);
};

const ResourceDialog = ({ request, accounts = [], onClose, onSubmit }: ResourceDialogProps) => {
  const styles = useStyles();
  const [draft, setDraft] = useState<AccountDraft | TaxonomyDraft | null>(null);
  const [openingBalance, setOpeningBalance] = useState("0");
  const [customColours, setCustomColours] = useState(false);
  const [touched, setTouched] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    const next = request === null ? null : initialDraft(request);
    setDraft(next);
    setOpeningBalance(
      next !== null && "currency" in next ? toMajorUnits(next.openingBalanceMinorUnits) : "0",
    );
    setCustomColours(
      next !== null && "priority" in next &&
      (next.foregroundHex !== null || next.backgroundHex !== null) &&
      matchingPreset(next.foregroundHex ?? null, next.backgroundHex ?? null) === null,
    );
    setError(null);
    setIsSaving(false);
  }, [request]);

  const close = () => {
    const trigger = request?.trigger;
    onClose();
    window.setTimeout(() => {
      if (trigger?.isConnected) trigger.focus();
    }, 0);
  };

  const submit = async () => {
    if (request === null) return;
    setTouched(true);
    if (request.action !== "delete" && (draft === null || !draft.label.trim())) return;
    const openingBalanceMinorUnits = parseMajorUnits(openingBalance);
    if (
      request.action === "create" &&
      request.resource.kind === "account" &&
      openingBalanceMinorUnits === null
    ) return;
    setError(null);
    setIsSaving(true);
    try {
      await onSubmit(
        request,
        request.action === "delete" || draft === null
          ? undefined
          : "currency" in draft
            ? {
                ...draft,
                label: draft.label.trim(),
                openingBalanceMinorUnits: openingBalanceMinorUnits ?? draft.openingBalanceMinorUnits,
                isDefault: defaultLocked ? true : draft.isDefault,
              }
            : { ...draft, label: draft.label.trim() },
      );
      close();
    } catch {
      setError("The change could not be saved. Try again.");
      setIsSaving(false);
    }
  };

  const open = request !== null;
  const name = request === null ? "resource" : resourceName(request.resource);
  const deleting = request?.action === "delete";
  const editingAccount = request?.resource.kind === "account";
  const editingCategory = request?.resource.kind === "category";
  const editingTag = request?.resource.kind === "tag";
  const accountDraft = draft !== null && "currency" in draft ? draft : null;
  const taxonomyDraft = draft !== null && "priority" in draft ? draft : null;
  const editedAccountId = request?.resource.kind === "account" ? request.resource.value.id : null;
  const otherAccounts = accounts.filter((account) => account.id !== editedAccountId);
  const currentDefault = otherAccounts.find((account) => account.isDefault === true) ?? null;
  const isFirstAccount = editingAccount && request?.action === "create" && accounts.length === 0;
  const isOnlyDefault = editingAccount && request?.action === "edit" && currentDefault === null;
  const defaultLocked = isFirstAccount === true || isOnlyDefault === true;
  const demotedAccount = accountDraft?.isDefault === true ? currentDefault : null;
  const labelError = !touched || deleting || (draft?.label.trim() ?? "") !== ""
    ? null
    : `Give this ${name} a name.`;
  const balanceError = !touched || deleting || !editingAccount || request?.action !== "create" ||
    parseMajorUnits(openingBalance) !== null
    ? null
    : "Enter an amount such as 250 or 250.75.";

  return <Dialog open={open} onOpenChange={(_event, data) => { if (!data.open && !isSaving) close(); }}>
    <DialogSurface>
      <DialogBody>
        <DialogTitle>{deleting ? `Delete ${name}` : `${request?.action === "edit" ? "Edit" : "Add"} ${name}`}</DialogTitle>
        <DialogContent>
          {deleting ? <p>Delete “{request?.resource.value.label}”? Any active {name} filter will be cleared, and affected transaction filters will no longer match this value.</p> : <div className={styles.form}>
            <Field
              label="Label"
              required
              validationState={labelError === null ? "none" : "error"}
              validationMessage={labelError ?? undefined}>
              <Input
                autoFocus
                value={draft?.label ?? ""}
                onBlur={() => setTouched(true)}
                onChange={(_event, data) => setDraft((current) => current === null ? current : { ...current, label: data.value })}
              />
            </Field>
            {editingAccount && accountDraft !== null && <>
              {request?.action === "create" && <>
                <Field
                  label="Opening balance"
                  required
                  hint={`How much this account holds today, in ${accountDraft.currency}.`}
                  validationState={balanceError === null ? "none" : "error"}
                  validationMessage={balanceError ?? undefined}>
                  <Input
                    inputMode="decimal"
                    value={openingBalance}
                    onBlur={() => setTouched(true)}
                    onChange={(_event, data) => setOpeningBalance(data.value)}
                  />
                </Field>
                <Field label="Currency" required hint="Fixed once the account exists.">
                  <Select
                    value={accountDraft.currency}
                    onChange={(_event, data) => setDraft({ ...accountDraft, currency: data.value as Currency })}>
                    {Object.values(Currency).map((currency) => (
                      <option key={currency} value={currency}>{currency}</option>
                    ))}
                  </Select>
                </Field>
              </>}
              <Checkbox
                checked={defaultLocked ? true : accountDraft.isDefault}
                disabled={defaultLocked}
                onChange={(_event, data) => setDraft({ ...accountDraft, isDefault: data.checked === true })}
                label="Default account"
              />
              {defaultLocked && <Caption1 className={styles.hint}>
                {isFirstAccount
                  ? "Your first account is the default one."
                  : "Make another account the default to move it off this one."}
              </Caption1>}
              {demotedAccount !== null && <MessageBar intent="warning">
                <MessageBarBody>
                  “{demotedAccount.label}” will stop being the default account.
                </MessageBarBody>
              </MessageBar>}
            </>}
            {editingCategory && taxonomyDraft !== null && (
              <Field label="How essential is it?" hint="Used to report what your spending was really for.">
                <Select
                  value={taxonomyDraft.priority ?? "Useful"}
                  onChange={(_event, data) => setDraft({ ...taxonomyDraft, priority: data.value as CategoryPriority })}>
                  {categoryPriorities.map((priority) => (
                    <option key={priority} value={priority}>{priority}</option>
                  ))}
                </Select>
              </Field>
            )}
            {editingTag && taxonomyDraft !== null && <>
              <Field label="Colour">
                <div className={styles.swatches} role="radiogroup" aria-label="Tag colour">
                  {tagPresetChoices.map((preset) => {
                    const chosen = !customColours &&
                      preset.backgroundHex.toLowerCase() === (taxonomyDraft.backgroundHex ?? "").toLowerCase() &&
                      preset.foregroundHex.toLowerCase() === (taxonomyDraft.foregroundHex ?? "").toLowerCase();
                    return <button
                      key={preset.name}
                      type="button"
                      role="radio"
                      aria-checked={chosen}
                      aria-label={preset.name}
                      className={mergeClasses(styles.swatch, chosen && styles.swatchChosen)}
                      style={{ backgroundColor: preset.backgroundHex, color: preset.foregroundHex }}
                      onClick={() => {
                        setCustomColours(false);
                        setDraft({ ...taxonomyDraft, backgroundHex: preset.backgroundHex, foregroundHex: preset.foregroundHex });
                      }}>
                      {taxonomyDraft.label.trim() === "" ? preset.name : taxonomyDraft.label}
                    </button>;
                  })}
                </div>
              </Field>
              <Checkbox
                checked={customColours}
                onChange={(_event, data) => setCustomColours(data.checked === true)}
                label="Choose my own colours"
              />
              {customColours && <div className={styles.colors}>
                <Field label="Background colour"><Input value={taxonomyDraft.backgroundHex ?? "#EDEDED"} onChange={(_event, data) => setDraft({ ...taxonomyDraft, backgroundHex: data.value })} /></Field>
                <Field label="Foreground colour"><Input value={taxonomyDraft.foregroundHex ?? "#242424"} onChange={(_event, data) => setDraft({ ...taxonomyDraft, foregroundHex: data.value })} /></Field>
              </div>}
            </>}
          </div>}
          {error !== null && <MessageBar intent="error"><MessageBarBody>{error}</MessageBarBody></MessageBar>}
        </DialogContent>
        <DialogActions>
          <Button appearance="secondary" disabled={isSaving} onClick={close}>Cancel</Button>
          <Button appearance={deleting ? "primary" : "primary"} disabled={isSaving} onClick={() => void submit()}>{deleting ? "Delete" : request?.action === "edit" ? "Save" : "Create"}</Button>
        </DialogActions>
      </DialogBody>
    </DialogSurface>
  </Dialog>;
};

export default ResourceDialog;
