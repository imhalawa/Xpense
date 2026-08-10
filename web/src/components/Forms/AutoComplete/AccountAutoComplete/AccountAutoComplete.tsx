import { useEffect, useState } from "react";
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

const AccountAutoComplete = ({
  label,
  value,
  error,
  helperText,
  options,
  onChange,
}: IAccountAutoCompleteProps) => {
  const styles = useStyles();
  const [selected, setSelected] = useState<IAccount | null>(
    value ?? options.find((account) => account.isDefault) ?? options[0] ?? null,
  );

  useEffect(() => {
    onChange(selected);
  }, [selected]);

  return (
    <Field
      label={label}
      required
      validationState={error ? "error" : "none"}
      validationMessage={helperText}>
      <Combobox
        value={selected?.label ?? ""}
        selectedOptions={selected === null ? [] : [selected.accountNumber]}
        onOptionSelect={(_event, data) =>
          setSelected(
            options.find((account) => account.accountNumber === data.optionValue) ?? null
          )
        }>
        {options.map((account) => (
          <Option key={account.accountNumber} value={account.accountNumber} text={account.label}>
            <span className={styles.option}>
              {account.label}
              {account.isDefault && <Badge appearance="tint">Main</Badge>}
            </span>
          </Option>
        ))}
      </Combobox>
    </Field>
  );
};

export default AccountAutoComplete;
