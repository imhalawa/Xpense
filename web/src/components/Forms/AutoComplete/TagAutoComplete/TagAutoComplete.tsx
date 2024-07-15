import { Autocomplete, createFilterOptions, TextField } from "@mui/material";
import { ITag } from "../../../../typings/ITag";
import { useEffect, useState } from "react";
import { createTagFixture } from "../../../../fixtures";

interface ITagAutoComplete {
  label: string;
  value: ITag[] | null;
  error?: boolean;
  helperText?: string;
  onChange: (value: ITag[] | null) => void;
}

const filter = createFilterOptions<ITag>();

const TagAutoComplete = ({ label, value, onChange, error, helperText }: ITagAutoComplete) => {
  const tags = createTagFixture();
  const [selected, setSelected] = useState<ITag[]>([]);

  useEffect(() => {
    onChange(selected);
  }, [selected, onChange]);

  return (
    <Autocomplete
      multiple
      freeSolo
      id="tags-Create"
      size="small"
      options={tags}
      onChange={(event, newValue, reason, details) => {
        if (details?.option.create && reason !== "removeOption") {
          setSelected([
            ...selected,
            {
              id: null,
              label: details.option.label,
              create: details.option.create,
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
        // Value selected with enter, right from the input
        if (typeof option === "string") {
          return `Add "${option}"`;
        }
        // Add "xxx" option created dynamically
        if (option.create) {
          return `Add "${option.label}"`;
        }
        // Regular option
        return option.label;
      }}
      renderOption={(props, option) => <li {...props}>{option.label}</li>}
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

export default TagAutoComplete;
