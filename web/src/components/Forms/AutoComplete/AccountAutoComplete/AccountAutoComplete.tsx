import { Autocomplete, Grid, TextField, Typography } from "@mui/material";
import { IAccount } from "../../../../typings/models/IAccount";
import { useEffect, useState } from "react";
import { listAccounts } from "../../../../clients/options";
import { useLoading } from "../../../../contexts/LoadingContext";

interface IAccountAutoCompleteProps {
  label: string;
  value: IAccount | null;
  error?: boolean;
  helperText?: string;
  onChange: (value: IAccount | null) => void;
}

const AccountAutoComplete = ({ label, value, error, helperText, onChange }: IAccountAutoCompleteProps) => {
  const { setLoading } = useLoading();

  const [accountOptions, setAccountOptions] = useState<IAccount[]>([]);
  const [selected, setSelected] = useState<IAccount | null>(null);

  useEffect(() => {
    setLoading(true);
    listAccounts()
      .then((accounts) => {
        setAccountOptions(accounts);
        setSelected(value || (accounts.find((account) => account.isDefault) ?? null));
        setLoading(false);
      })
      .catch((error) => {
        console.error(error);
        setLoading(false);
      });
  }, []);

  useEffect(() => {
    onChange(selected);
  }, [selected]);

  return (
    <Autocomplete
      id="account-autocomplete"
      options={accountOptions}
      isOptionEqualToValue={(option, selectedOption) => option.accountNumber === selectedOption.accountNumber}
      autoHighlight
      value={selected}
      onChange={(_, newValue: IAccount | null) => setSelected(newValue)}
      getOptionLabel={(option) => option.label}
      renderOption={(props, option) => {
        const { key, ...optionProps } = props;
        return (
          <Grid container spacing={1} key={key} component="li" {...optionProps}>
            <Grid size={10}>
              <Typography variant="body2">{option.label}&nbsp;</Typography>
            </Grid>
            <Grid size={2}>
              {option.isDefault && (
                <Typography variant="body2" color="success.main">
                  Main
                </Typography>
              )}
            </Grid>
          </Grid>
        );
      }}
      renderInput={(params) => (
        <TextField
          {...params}
          required
          label={label}
          value={selected}
          error={error}
          helperText={helperText}
          slotProps={{
            ...params.slotProps,

            htmlInput: {
              ...params.slotProps.htmlInput,
              autoComplete: "new-password",
            }
          }}
        />
      )}
    />
  );
};

export default AccountAutoComplete;
