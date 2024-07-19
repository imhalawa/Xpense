import { Autocomplete, createFilterOptions, TextField } from "@mui/material";
import { ITag } from "../../../../typings/models/ITag";
import { useEffect, useState } from "react";
import axios from "axios";
import { IResponse } from "../../../../clients/types/IResponse";
import { useLoading } from "../../../../contexts/LoadingContext";

interface ITagAutoCompleteProps {
  label: string;
  value: ITag[] | null;
  error?: boolean;
  helperText?: string;
  onChange: (value: ITag[] | null) => void;
}

const filter = createFilterOptions<ITag>();

const TagAutoComplete = ({ label, value, onChange, error, helperText }: ITagAutoCompleteProps) => {
  const { setLoading } = useLoading();

  const [tagOptions, setTagOptions] = useState<ITag[]>([]);
  const [selected, setSelected] = useState<ITag[]>([]);

  useEffect(() => {
    onChange(selected);
  }, [selected]);

  useEffect(() => {
    setLoading(true);
    axios.defaults.baseURL = "http://localhost:4000/";
    axios
      .get<IResponse<ITag[]>>("/api/tag")
      .then((response) => {
        setTagOptions(response.data.data);
        setLoading(false);
      })
      .catch((error) => {
        console.error(error);
        setLoading(false);
      });
  }, []);

  return (
    <Autocomplete
      multiple
      freeSolo
      id="tags-Create"
      size="small"
      options={tagOptions}
      onChange={(event, newValue, reason, details) => {
        if (details?.option.create && reason !== "removeOption") {
          setSelected([
            ...selected,
            {
              id: null,
              label: details.option.label,
              create: details.option.create,
              createdOn: details.option.createdOn,
              lastUpdated: details.option.lastUpdated,
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
                  createdOn: 0,
                  lastUpdated: 0,
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
        // Suggest the creation of a new value
        const isExisting = options.some((option) => inputValue === option.label);
        if (inputValue !== "" && !isExisting) {
          filtered.push({
            id: null,
            label: inputValue,
            create: true,
            createdOn: 0,
            lastUpdated: 0,
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
        // Value selected with enter, right from the input
        if (typeof option === "string") {
          return option;
        }
        // Add "xxx" option created dynamically
        if (option.create) {
          return option.label;
        }
        // Regular option
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

export default TagAutoComplete;
