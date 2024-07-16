import { Autocomplete, createFilterOptions, TextField } from "@mui/material";
import { useState } from "react";
import { createMerchantFixture } from "../../../../fixtures";
import { IMerchant } from "../../../../typings";

interface IMerchantAutoCompleteProps {
  label: string;
  value: IMerchant | null;
  error?: boolean;
  helperText?: string;
  onChange: (value: IMerchant | null) => void;
}

const filter = createFilterOptions<IMerchant>();

const MerchantAutoComplete = ({ label, value, onChange, error, helperText }: IMerchantAutoCompleteProps) => {
  const [merchantOptions, setMerchantOptions] = useState<IMerchant[]>(createMerchantFixture());

  return (
    <Autocomplete
      freeSolo
      id="tags-Create"
      size="small"
      options={merchantOptions}
      onChange={(event, newValue, reason, details) => {
        if (typeof newValue === "string") {
          onChange({
            id: null,
            label: newValue,
            create: true,
          });
        } else if (newValue && newValue.create) {
          // Create a new value from the user input
          onChange({
            id: null,
            label: newValue.label,
            create: true,
          });
        } else {
          onChange(newValue);
        }
      }}
      filterSelectedOptions
      filterOptions={(options, params): IMerchant[] => {
        const filtered = filter(options, params);

        const { inputValue } = params;
        // Suggest the creation of a new value
        const isExisting = options.some((option) => inputValue === option.label);
        if (inputValue !== "" && !isExisting) {
          filtered.push({
            id: null,
            label: inputValue,
            create: true,
          });
        }
        return filtered;
      }}
      selectOnFocus
      clearOnBlur
      handleHomeEndKeys
      getOptionLabel={(option) => {
        if (typeof option === "string") {
          return option;
        }
        if (option.create) {
          return option.label;
        }
        return option.label;
      }}
      renderOption={(props, option) => (
        <li {...props} key={option.id}>
          {option.label}
        </li>
      )}
      renderInput={(params) => (
        <TextField
          {...params}
          label={label}
          value={value}
          variant="standard"
          placeholder={label}
          error={error}
          helperText={helperText}
        />
      )}
    />
  );
};

export default MerchantAutoComplete;
