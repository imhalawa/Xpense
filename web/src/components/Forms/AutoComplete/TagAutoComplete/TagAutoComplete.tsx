import { Autocomplete, TextField } from "@mui/material";
import { ITag } from "../../../../typings/ITag";

interface ITagAutoComplete {
  options: ITag[];
  label: string;
  onChange: (value: ITag[] | null) => void;
}

const TagAutoComplete = ({ options, label, onChange }: ITagAutoComplete) => {
  return (
    <Autocomplete
      multiple
      id="size-small-filled"
      size="small"
      options={options}
      onChange={(event, value) => onChange(value)}
      getOptionLabel={(option) => option.label}
      renderInput={(params) => (
        <TextField {...params} label={label} placeholder={label} />
      )}
    />
  );
};

export default TagAutoComplete;
