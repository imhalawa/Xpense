import { Autocomplete, Grid, TextField, Typography } from "@mui/material";
import { IAccount } from "../../../../typings/models/IAccount";
import { createAccountFixture } from "../../../../fixtures";
import { useEffect, useState } from "react";

interface IAccountAutoCompleteProps {
  label: string;
  value: IAccount | null;
  error?: boolean;
  helperText?: string;
  onChange: (value: IAccount | null) => void;
}

const AccountAutoComplete = ({ label, value, error, helperText, onChange }: IAccountAutoCompleteProps) => {
  const accounts = createAccountFixture();
  const defaultAccount = value || (accounts.find((a) => a.isDefault) ?? null);

  const [selected, setSelected] = useState<IAccount | null>(defaultAccount);

  useEffect(() => {
    onChange(selected);
  }, [selected]);

  return (
    <Autocomplete
      id="account-autocomplete"
      options={accounts}
      isOptionEqualToValue={(a, b) => a.id === b.id}
      autoHighlight
      value={defaultAccount}
      onChange={(event: any, newValue: IAccount | null) => setSelected(newValue)}
      getOptionLabel={(option) => option.label}
      renderOption={(props, option) => {
        const { key, ...optionProps } = props;
        return (
          <Grid container spacing={1} key={key} component="li" {...optionProps}>
            <Grid item xs={10}>
              <Typography variant="body2">{option.label}&nbsp;</Typography>
            </Grid>
            <Grid item xs={2}>
              {option.isDefault && (
                <Typography variant="body2" color="green">
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
          label={label}
          variant="standard"
          value={selected}
          inputProps={{
            ...params.inputProps,
            autoComplete: "new-password", // disable autocomplete and autofill
          }}
          error={error}
          helperText={helperText}
        />
      )}
    />
  );
};

export default AccountAutoComplete;
