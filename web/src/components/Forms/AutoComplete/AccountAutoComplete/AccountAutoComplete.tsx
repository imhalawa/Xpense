import { useEffect, useState } from "react";
import { Badge, Combobox, Field, Option, makeStyles, tokens } from "@fluentui/react-components";
import { IAccount } from "../../../../typings/models/IAccount";
import { listAccounts } from "../../../../clients/options";
import { useLoading } from "../../../../contexts/LoadingContext";

interface IAccountAutoCompleteProps {
  label: string;
  value: IAccount | null;
  error?: boolean;
  helperText?: string;
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
  onChange,
}: IAccountAutoCompleteProps) => {
  const styles = useStyles();
  const { setLoading } = useLoading();
  const [accountOptions, setAccountOptions] = useState<IAccount[]>([]);
  const [selected, setSelected] = useState<IAccount | null>(value);

  useEffect(() => {
    setLoading(true);
    listAccounts()
      .then((accounts) => {
        setAccountOptions(accounts);
        setSelected(value ?? accounts.find((account) => account.isDefault) ?? null);
        setLoading(false);
      })
      .catch((loadError) => {
        console.error(loadError);
        setLoading(false);
      });
  }, []);

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
            accountOptions.find((account) => account.accountNumber === data.optionValue) ?? null
          )
        }>
        {accountOptions.map((account) => (
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
