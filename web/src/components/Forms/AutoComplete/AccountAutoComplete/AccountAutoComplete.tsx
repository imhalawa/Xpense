import { Autocomplete, Grid, TextField, Typography } from "@mui/material";
import { IAccount } from "../../../../typings/models/IAccount";
import { useEffect, useState } from "react";
import axios from "axios";
import { IResponse } from "../../../../clients/types/IResponse";
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
    // TODO: need to clean up this later
    axios
      .get<IResponse<IAccount[]>>("/api/account")
      .then((response) => {
        setAccountOptions(response.data.data);
        setSelected(value || (response.data.data.find((a) => a.isDefault) ?? null));
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
      isOptionEqualToValue={(a, b) => a.id === b.id}
      autoHighlight
      value={selected}
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
          required
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
