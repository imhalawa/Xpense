import { Autocomplete, createFilterOptions, TextField } from "@mui/material";
import { ITag } from "../../../../typings/models/ITag";
import { useEffect, useState } from "react";
import { listTags } from "../../../../clients/options";
import { useLoading } from "../../../../contexts/LoadingContext";
import { useDebouncedValue } from "../../../../hooks/useDebouncedValue";

interface ITagAutoCompleteProps {
  label: string;
  value: ITag[] | null;
  error?: boolean;
  helperText?: string;
  onChange: (value: ITag[] | null) => void;
}

const filter = createFilterOptions<ITag>();

const searchDelayMilliseconds = 250;

const TagAutoComplete = ({ label, value, onChange, error, helperText }: ITagAutoCompleteProps) => {
  const { setLoading } = useLoading();

  const [tagOptions, setTagOptions] = useState<ITag[]>([]);
  const [selected, setSelected] = useState<ITag[]>([]);
  const [search, setSearch] = useState("");
  const [searching, setSearching] = useState(false);
  const debouncedSearch = useDebouncedValue(search, searchDelayMilliseconds);

  useEffect(() => {
    onChange(selected);
  }, [selected]);

  useEffect(() => {
    if (!searching) return;

    let stale = false;
    setLoading(true);
    listTags({ search: debouncedSearch })
      .then((tags) => {
        if (stale) return;
        setTagOptions(tags.map((tag) => ({ ...tag, create: false })));
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

  return (
    <Autocomplete
      multiple
      freeSolo
      id="tags-Create"
      options={tagOptions}
      onInputChange={(_event, newInputValue) => {
        setSearching(true);
        setSearch(newInputValue);
      }}
      onChange={(_, newValue, reason, details) => {
        if (details?.option.create && reason !== "removeOption") {
          setSelected([
            ...selected,
            {
              id: null,
              label: details.option.label,
              create: details.option.create,
              createdAt: details.option.createdAt,
              updatedAt: details.option.updatedAt,
              fgColorHex: details.option.fgColorHex,
              bgColorHex: details.option.bgColorHex,
            },
          ]);
        } else {
          setSelected(
            newValue.map((value) => {
              if (typeof value === "string") {
                return {
                  id: null,
                  label: value,
                  create: true,
                  createdAt: null,
                  updatedAt: null,
                  fgColorHex: "",
                  bgColorHex: "",
                };
              } else {
                return value;
              }
            })
          );
        }
      }}
      filterSelectedOptions
      filterOptions={(options, params): ITag[] => {
        const filtered = filter(options, params);

        const { inputValue } = params;
        const isExisting = options.some((option) => inputValue === option.label);
        if (inputValue !== "" && !isExisting) {
          filtered.push({
            id: null,
            label: inputValue,
            create: true,
            createdAt: null,
            updatedAt: null,
            fgColorHex: "",
            bgColorHex: "",
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

export default TagAutoComplete;
