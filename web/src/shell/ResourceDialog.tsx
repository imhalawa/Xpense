import { useEffect, useState } from "react";
import {
  Button,
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
  Option,
  Select,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { Currency } from "../typings/enums/Currency";
import type { AccountDraft, CategoryPriority, TaxonomyDraft } from "../vault/VaultProjection";
import type { SidebarResource, SidebarResourceAction } from "./SidebarFilters";

export interface ResourceDialogRequest {
  action: SidebarResourceAction;
  resource: SidebarResource;
  trigger: HTMLElement;
}

interface ResourceDialogProps {
  request: ResourceDialogRequest | null;
  onClose: () => void;
  onSubmit: (request: ResourceDialogRequest, draft?: AccountDraft | TaxonomyDraft) => Promise<void>;
}

const useStyles = makeStyles({
  form: { display: "flex", flexDirection: "column", gap: tokens.spacingVerticalM },
  colors: { display: "flex", gap: tokens.spacingHorizontalM },
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

const ResourceDialog = ({ request, onClose, onSubmit }: ResourceDialogProps) => {
  const styles = useStyles();
  const [draft, setDraft] = useState<AccountDraft | TaxonomyDraft | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    setDraft(request === null ? null : initialDraft(request));
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
    if (request.action !== "delete" && (draft === null || !draft.label.trim())) {
      setError("Enter a label before saving.");
      return;
    }
    setError(null);
    setIsSaving(true);
    try {
      await onSubmit(
        request,
        request.action === "delete" || draft === null ? undefined : { ...draft, label: draft.label.trim() },
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

  return <Dialog open={open} onOpenChange={(_event, data) => { if (!data.open && !isSaving) close(); }}>
    <DialogSurface>
      <DialogBody>
        <DialogTitle>{deleting ? `Delete ${name}` : `${request?.action === "edit" ? "Edit" : "Add"} ${name}`}</DialogTitle>
        <DialogContent>
          {deleting ? <p>Delete “{request?.resource.value.label}”? Any active {name} filter will be cleared, and affected transaction filters will no longer match this value.</p> : <div className={styles.form}>
            <Field label="Label" required><Input autoFocus value={draft?.label ?? ""} onChange={(_event, data) => setDraft((current) => current === null ? current : { ...current, label: data.value })} /></Field>
            {editingAccount && accountDraft !== null && <>
              {request?.action === "create" && <>
                <Field label="Opening balance in minor units" required><Input type="number" value={String(accountDraft.openingBalanceMinorUnits)} onChange={(_event, data) => setDraft({ ...accountDraft, openingBalanceMinorUnits: Number(data.value) })} /></Field>
                <Field label="Currency"><Select value={accountDraft.currency} onChange={(_event, data) => setDraft({ ...accountDraft, currency: data.value as Currency })}>{Object.values(Currency).map((currency) => <Option key={currency} value={currency}>{currency}</Option>)}</Select></Field>
              </>}
              <Checkbox checked={accountDraft.isDefault} onChange={(_event, data) => setDraft({ ...accountDraft, isDefault: data.checked === true })} label="Default account" />
            </>}
            {editingCategory && taxonomyDraft !== null && <Field label="Priority"><Select value={taxonomyDraft.priority ?? "Useful"} onChange={(_event, data) => setDraft({ ...taxonomyDraft, priority: data.value as CategoryPriority })}>{categoryPriorities.map((priority) => <Option key={priority} value={priority}>{priority}</Option>)}</Select></Field>}
            {editingTag && taxonomyDraft !== null && <div className={styles.colors}>
              <Field label="Background colour"><Input value={taxonomyDraft.backgroundHex ?? "#EDEDED"} onChange={(_event, data) => setDraft({ ...taxonomyDraft, backgroundHex: data.value })} /></Field>
              <Field label="Foreground colour"><Input value={taxonomyDraft.foregroundHex ?? "#242424"} onChange={(_event, data) => setDraft({ ...taxonomyDraft, foregroundHex: data.value })} /></Field>
            </div>}
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
