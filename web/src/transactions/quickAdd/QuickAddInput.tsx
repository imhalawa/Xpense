import { ChangeEvent, ComponentProps, useEffect, useMemo, useRef } from "react";
import { Caption1, Label, makeStyles, mergeClasses, tokens } from "@fluentui/react-components";
import { parseQuickAdd } from "./parser";
import { QuickAddField, QuickAddParseResult, QuickAddParserContext, QuickAddRange } from "./types";

const fieldNames: Record<QuickAddField, string> = {
  kind: "Type",
  amount: "Amount",
  currency: "Currency",
  merchant: "Merchant",
  category: "Category",
  sourceAccount: "From",
  destinationAccount: "To",
  tag: "Tag",
  date: "Date",
  time: "Time",
  reason: "Reason",
  text: "Text",
};

const useStyles = makeStyles({
  root: {
    display: "grid",
    rowGap: tokens.spacingVerticalXS,
  },
  editor: {
    position: "relative",
    minHeight: "40px",
    border: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke1}`,
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorNeutralBackground1,
    boxSizing: "border-box",
    overflow: "hidden",
    ":focus-within": {
      borderBottomColor: tokens.colorBrandStroke1,
      borderBottomWidth: tokens.strokeWidthThick,
    },
  },
  input: {
    position: "relative",
    zIndex: 2,
    width: "100%",
    minHeight: "40px",
    paddingBlock: tokens.spacingVerticalS,
    paddingInline: tokens.spacingHorizontalM,
    border: 0,
    outline: 0,
    boxSizing: "border-box",
    backgroundColor: "transparent",
    color: "transparent",
    caretColor: tokens.colorNeutralForeground1,
    fontFamily: tokens.fontFamilyBase,
    fontSize: tokens.fontSizeBase300,
    lineHeight: tokens.lineHeightBase300,
    "::placeholder": {
      color: tokens.colorNeutralForeground3,
    },
    "::selection": {
      backgroundColor: tokens.colorBrandBackground2,
    },
  },
  decoration: {
    position: "absolute",
    inset: 0,
    zIndex: 1,
    paddingBlock: tokens.spacingVerticalS,
    paddingInline: tokens.spacingHorizontalM,
    boxSizing: "border-box",
    overflow: "hidden",
    pointerEvents: "none",
    whiteSpace: "pre",
    color: tokens.colorNeutralForeground1,
    fontFamily: tokens.fontFamilyBase,
    fontSize: tokens.fontSizeBase300,
    lineHeight: tokens.lineHeightBase300,
  },
  recognized: {
    color: tokens.colorBrandForeground1,
    backgroundColor: tokens.colorBrandBackground2,
    borderRadius: tokens.borderRadiusSmall,
    boxShadow: `inset 0 -1px ${tokens.colorBrandStroke1}`,
  },
  unresolved: {
    textDecorationLine: "underline",
    textDecorationStyle: "dotted",
    textDecorationColor: tokens.colorNeutralForeground3,
  },
  invalid: {
    color: tokens.colorStatusDangerForeground1,
    backgroundColor: tokens.colorStatusDangerBackground1,
    textDecorationLine: "underline",
    textDecorationStyle: "wavy",
  },
  chips: {
    display: "flex",
    flexWrap: "wrap",
    gap: tokens.spacingHorizontalXS,
    minHeight: "24px",
  },
  chip: {
    minHeight: "24px",
    paddingBlock: 0,
    paddingInline: tokens.spacingHorizontalS,
    border: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusCircular,
    backgroundColor: tokens.colorNeutralBackground2,
    color: tokens.colorNeutralForeground2,
    fontFamily: tokens.fontFamilyBase,
    fontSize: tokens.fontSizeBase200,
    cursor: "pointer",
    ":focus-visible": {
      outline: `${tokens.strokeWidthThick} solid ${tokens.colorBrandStroke1}`,
      outlineOffset: "1px",
    },
  },
  chipError: {
    color: tokens.colorStatusDangerForeground1,
    border: `${tokens.strokeWidthThin} solid ${tokens.colorStatusDangerForeground1}`,
  },
  summary: {
    color: tokens.colorNeutralForeground3,
  },
  live: {
    position: "absolute",
    width: "1px",
    height: "1px",
    padding: 0,
    margin: "-1px",
    overflow: "hidden",
    clip: "rect(0, 0, 0, 0)",
    whiteSpace: "nowrap",
    border: 0,
  },
});

interface QuickAddInputProps
  extends Omit<ComponentProps<"input">, "value" | "onChange" | "children"> {
  value: string;
  context: QuickAddParserContext;
  onValueChange: (value: string) => void;
  onParseResult?: (result: QuickAddParseResult) => void;
  label?: string;
}

const decoratedContent = (input: string, ranges: QuickAddRange[], styles: ReturnType<typeof useStyles>) => {
  const content = [];
  let cursor = 0;
  ranges.forEach((range) => {
    if (range.start > cursor) content.push(input.slice(cursor, range.start));
    const className =
      range.status === "recognized"
        ? styles.recognized
        : range.status === "unresolved"
          ? styles.unresolved
          : styles.invalid;
    content.push(
      <span key={range.id} className={className} data-field={range.field}>
        {input.slice(range.start, range.end)}
      </span>
    );
    cursor = range.end;
  });
  if (cursor < input.length) content.push(input.slice(cursor));
  return content;
};

export const QuickAddInput = ({
  value,
  context,
  onValueChange,
  onParseResult,
  label = "Quick Add",
  id = "quick-add-transaction",
  placeholder = "Spent 5 euros at Albert Heijn #shopping",
  ...inputProps
}: QuickAddInputProps) => {
  const styles = useStyles();
  const inputReference = useRef<HTMLInputElement>(null);
  const result = useMemo(() => parseQuickAdd(value, context), [context, value]);

  useEffect(() => {
    onParseResult?.(result);
  }, [onParseResult, result]);

  const focusRange = (range: QuickAddRange) => {
    inputReference.current?.focus();
    inputReference.current?.setSelectionRange(range.start, range.end);
  };

  return (
    <div className={styles.root}>
      <Label htmlFor={id}>{label}</Label>
      <div className={styles.editor}>
        <div className={styles.decoration} aria-hidden="true" data-testid="quick-add-decoration">
          {decoratedContent(value, result.ranges, styles)}
        </div>
        <input
          {...inputProps}
          ref={inputReference}
          id={id}
          className={styles.input}
          value={value}
          placeholder={placeholder}
          autoComplete="off"
          spellCheck={false}
          aria-describedby={`${id}-summary`}
          onChange={(event: ChangeEvent<HTMLInputElement>) => onValueChange(event.target.value)}
        />
      </div>
      <div className={styles.chips} aria-label="Recognised Quick Add fields">
        {result.ranges.map((range) => (
          <button
            key={range.id}
            type="button"
            className={mergeClasses(styles.chip, range.status !== "recognized" && styles.chipError)}
            aria-label={`${fieldNames[range.field]}: ${range.text}, ${range.status}`}
            onClick={() => focusRange(range)}>
            {fieldNames[range.field]} · {range.text}
          </button>
        ))}
      </div>
      <Caption1 id={`${id}-summary`} className={styles.summary}>
        {result.issues.length === 0
          ? "Ready to review in the transaction form"
          : result.issues[0]?.message}
      </Caption1>
      <span className={styles.live} role="status" aria-live="polite" aria-atomic="true">
        {result.announcement}
      </span>
    </div>
  );
};
