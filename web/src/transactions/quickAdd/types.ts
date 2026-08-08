import { Currency } from "../../typings/enums/Currency";

export type QuickAddKind = "expense" | "income" | "transfer";

export type QuickAddField =
  | "kind"
  | "amount"
  | "currency"
  | "merchant"
  | "category"
  | "sourceAccount"
  | "destinationAccount"
  | "tag"
  | "date"
  | "time"
  | "reason"
  | "text";

export type QuickAddRangeStatus = "recognized" | "unresolved" | "conflict" | "invalid";

export interface QuickAddRange {
  id: string;
  start: number;
  end: number;
  text: string;
  field: QuickAddField;
  label: string;
  status: QuickAddRangeStatus;
}

export interface QuickAddIssue {
  code: string;
  field: QuickAddField;
  message: string;
  severity: "error" | "warning";
  rangeIds: string[];
  candidates?: string[];
}

export interface QuickAddAccount {
  id: string;
  label: string;
  currency: Currency;
  aliases?: string[];
}

export interface QuickAddOption {
  id: string;
  label: string;
}

export interface QuickAddReference extends QuickAddOption {
  create: boolean;
}

export type QuickAddCategoryPriority = "Low" | "Medium" | "High";

export type QuickAddCategoryReference =
  | (QuickAddOption & {
      create: true;
      priority: QuickAddCategoryPriority;
    })
  | (QuickAddOption & {
      create: false;
      priority?: never;
    });

export interface QuickAddParserContext {
  accounts: QuickAddAccount[];
  categories: QuickAddOption[];
  merchants: QuickAddOption[];
  tags: QuickAddOption[];
  defaultAccountId?: string;
  now?: Date;
  supportedCurrencies?: Currency[];
  decimalSeparator?: "." | ",";
}

export interface QuickAddDraft {
  kind: QuickAddKind | null;
  amountMinorUnits: number | null;
  currency: Currency | null;
  occurredAt: string | null;
  sourceAccount: QuickAddAccount | null;
  destinationAccount: QuickAddAccount | null;
  merchant: QuickAddReference | null;
  category: QuickAddCategoryReference | null;
  tags: QuickAddReference[];
  reason: string | null;
}

export interface QuickAddParseResult {
  input: string;
  draft: QuickAddDraft;
  ranges: QuickAddRange[];
  issues: QuickAddIssue[];
  canSubmit: boolean;
  announcement: string;
}

export interface QuickAddPickerRequest {
  range: QuickAddRange;
  result: QuickAddParseResult;
  rewrite: (replacement: string) => void;
}
