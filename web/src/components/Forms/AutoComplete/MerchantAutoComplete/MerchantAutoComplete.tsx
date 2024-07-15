import { Autocomplete, Grid, TextField, Typography } from "@mui/material";
import { IMerchant } from "../../../../typings";
import { createMerchantFixture } from "../../../../fixtures";

interface IMerchantAutoCompleteProps {
  label: string;
  value: IMerchant | null;
  error?: boolean;
  helperText?: string;
  onChange: (value: IMerchant | null) => void;
}

const MerchantAutoComplete = ({ label, value, error, helperText, onChange }: IMerchantAutoCompleteProps) => {
  const merchants = createMerchantFixture();

  return (
    <Autocomplete
      id="select-merchant"
      options={merchants}
      isOptionEqualToValue={(a, b) => a.id === b.id}
      autoHighlight
      onChange={(event: any, newValue: IMerchant | null) => {
        event.preventDefault();
        onChange(newValue);
      }}
      getOptionLabel={(option) => option.label}
      renderOption={(props, option) => {
        const { key, ...optionProps } = props;
        return (
          <Grid container spacing={1} key={key} component="li" {...optionProps}>
            <Grid item xs={12}>
              <Typography variant="body2">{option.label}&nbsp;</Typography>
            </Grid>
          </Grid>
        );
      }}
      renderInput={(params) => (
        <TextField
          {...params}
          label={label}
          variant="standard"
          inputProps={{
            ...params.inputProps,
            autoComplete: "new-password", // disable autocomplete and autofill
          }}
          value={value}
          error={error}
          helperText={helperText}
        />
      )}
    />
  );
};

export default MerchantAutoComplete;
