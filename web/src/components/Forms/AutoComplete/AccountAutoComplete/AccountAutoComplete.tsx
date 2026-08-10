import { useEffect, useRef, useState } from "react";
import { Badge, Combobox, Field, Option, makeStyles, tokens } from "@fluentui/react-components";
import { IAccount } from "../../../../typings/models/IAccount";

interface IAccountAutoCompleteProps {
  label: string;
  value: IAccount | null;
  error?: boolean;
  helperText?: string;
  options: IAccount[];
  onChange: (value: IAccount | null) => void;
}

const useStyles = makeStyles({
  option: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    width: "100%",
    gap: tokens.spacingHorizontalM,
  },
});

const preferredAccount = (options: IAccount[]): IAccount | null =>
  options.find((account) => account.isDefault) ?? options[0] ?? null;

const AccountAutoComplete = ({
  label,
  value,
  error,
  helperText,
  options,
  onChange,
}: IAccountAutoCompleteProps) => {
  const styles = useStyles();
  const [query, setQuery] = useState<string | null>(null);
  const hasProposedDefault = useRef(false);

  useEffect(() => {
    if (hasProposedDefault.current || value !== null) return;
    const preferred = preferredAccount(options);
    if (preferred === null) return;
    hasProposedDefault.current = true;
    onChange(preferred);
  }, [onChange, options, value]);

  const matches = query === null
    ? options
    : options.filter((account) => account.label.toLowerCase().includes(query.toLowerCase()));

  return (
    <Field
      label={label}
      required
      validationState={error ? "error" : "none"}
      validationMessage={helperText}>
      <Combobox
        freeform
        value={query ?? value?.label ?? ""}
        selectedOptions={value === null ? [] : [value.accountNumber]}
        onChange={(event) => setQuery(event.target.value)}
        onBlur={() => setQuery(null)}
        onOptionSelect={(_event, data) => {
          setQuery(null);
          onChange(options.find((account) => account.accountNumber === data.optionValue) ?? null);
        }}>
        {matches.map((account) => (
          <Option key={account.accountNumber} value={account.accountNumber} text={account.label}>
            <span className={styles.option}>
              {account.label}
              {account.isDefault && <Badge appearance="tint">Main</Badge>}
            </span>
          </Option>
        ))}
        {matches.length === 0 && (
          <Option value="" text="" disabled>
            No account matches “{query}”
          </Option>
        )}
      </Combobox>
    </Field>
  );
};

export default AccountAutoComplete;
