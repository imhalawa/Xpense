import { Autocomplete, createFilterOptions, TextField } from "@mui/material";
import { useEffect, useState } from "react";
import { IMerchant } from "../../../../typings";
import { IResponse } from "../../../../clients/types/IResponse";
import axios from "axios";
import { useLoading } from "../../../../contexts/LoadingContext";

interface IMerchantAutoCompleteProps {
  label: string;
  value: IMerchant | null;
  error?: boolean;
  helperText?: string;
  onChange: (value: IMerchant | null) => void;
}

const filter = createFilterOptions<IMerchant>();

const MerchantAutoComplete = ({ label, value, onChange, error, helperText }: IMerchantAutoCompleteProps) => {
  const { setLoading } = useLoading();

  const [merchantOptions, setMerchantOptions] = useState<IMerchant[]>([]);
  const [selected, setSelected] = useState<IMerchant | null>(null);

  useEffect(() => {
    setLoading(true);
    axios
      .get<IResponse<IMerchant[]>>("/api/merchant")
      .then((response) => {
        setMerchantOptions(response.data.data);
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
      freeSolo
      id="tags-Create"
      size="small"
      options={merchantOptions}
      onChange={(_event, newValue, _reason, _details) => {
        if (typeof newValue === "string") {
          setSelected({
            id: null,
            label: newValue,
            create: true,
          });
        } else if (newValue && newValue.create) {
          // Create a new value from the user input
          setSelected({
            id: null,
            label: newValue.label,
            create: true,
          });
        } else {
          setSelected(newValue);
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
          required
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
