import { Autocomplete, createFilterOptions, TextField } from "@mui/material";
import { useEffect, useState } from "react";
import { IMerchant } from "../../../../typings";
import { listMerchants } from "../../../../clients/options";
import { useLoading } from "../../../../contexts/LoadingContext";
import { useDebouncedValue } from "../../../../hooks/useDebouncedValue";

interface IMerchantAutoCompleteProps {
  label: string;
  value: IMerchant | null;
  error?: boolean;
  helperText?: string;
  onChange: (value: IMerchant | null) => void;
}

const filter = createFilterOptions<IMerchant>();

const searchDelayMilliseconds = 250;

const MerchantAutoComplete = ({ label, value, onChange, error, helperText }: IMerchantAutoCompleteProps) => {
  const { setLoading } = useLoading();

  const [merchantOptions, setMerchantOptions] = useState<IMerchant[]>([]);
  const [selected, setSelected] = useState<IMerchant | null>(null);
  const [search, setSearch] = useState("");
  const [searching, setSearching] = useState(false);
  const debouncedSearch = useDebouncedValue(search, searchDelayMilliseconds);

  useEffect(() => {
    if (!searching) return;

    let stale = false;
    setLoading(true);
    listMerchants({ search: debouncedSearch })
      .then((merchants) => {
        if (stale) return;
        setMerchantOptions(merchants);
        setLoading(false);
      })
      .catch((error) => {
        if (stale) return;
        console.error(error);
        setLoading(false);
      });

    return () => {
      stale = true;
    };
  }, [searching, debouncedSearch]);

  useEffect(() => {
    onChange(selected);
  }, [selected]);

  return (
    <Autocomplete
      freeSolo
      id="tags-Create"
      options={merchantOptions}
      onInputChange={(_event, newInputValue) => {
        setSearching(true);
        setSearch(newInputValue);
      }}
      onChange={(_event, newValue, _reason, _details) => {
        if (typeof newValue === "string") {
          setSelected({
            id: null,
            label: newValue,
            create: true,
          });
        } else if (newValue && newValue.create) {
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
          onFocus={() => setSearching(true)}
          label={label}
          value={value}
          placeholder={label}
          error={error}
          helperText={helperText}
        />
      )}
    />
  );
};

export default MerchantAutoComplete;
