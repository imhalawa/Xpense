import { Autocomplete, Grid, TextField, Typography } from "@mui/material";
import { useEffect, useState } from "react";
import { ICategory, IPriority } from "../../../../typings";
import { IResponse } from "../../../../clients/types/IResponse";
import axios from "axios";
import { useLoading } from "../../../../contexts/LoadingContext";

interface ICategoryAutoCompleteProps {
  label: string;
  value: ICategory | null;
  error?: boolean;
  helperText?: string;
  onChange: (value: ICategory | null) => void;
}
const CategoryAutoComplete = ({ label, value, error, helperText, onChange }: ICategoryAutoCompleteProps) => {
  const { setLoading } = useLoading();
  const [categoryOptions, setCategoryOptions] = useState<ICategory[]>([]);
  const [selected, setSelected] = useState<ICategory | null>(null);

  //TODO: Later, give the user the option to pick a color for the priority
  const priorityColor = (priority: IPriority): string => {
    switch (priority.weight) {
      case 1:
        return "green";
      case 2:
        return "orange";
      case 3:
        return "red";
      default:
        return "Mauve";
    }
  };

  useEffect(() => {
    setLoading(true);
    axios.defaults.baseURL = "http://localhost:4000/";
    axios
      .get<IResponse<ICategory[]>>("/api/category")
      .then((response) => response.data)
      .then((response) => {
        setCategoryOptions(response.data);
        setSelected(value || (response.data.sort((a, b) => b.priority.weight - a.priority.weight)[0] ?? null));
        setLoading(false);
      })
      .catch((error) => {
        console.error(error);
        setLoading(false);
      });
  }, []);

  useEffect(() => {
    console.log(selected);
    onChange(selected);
  }, [selected]);

  return (
    <Autocomplete
      id="category-autocomplete"
      options={categoryOptions}
      isOptionEqualToValue={(a, b) => a.id === b.id}
      autoHighlight
      value={selected}
      onChange={(event: any, newValue: ICategory | null) => setSelected(newValue)}
      getOptionLabel={(option) => option.label}
      renderOption={(props, option) => {
        const { key, ...optionProps } = props;
        return (
          <Grid container spacing={1} key={key} component="li" {...optionProps}>
            <Grid item xs={10}>
              <Typography variant="body2">{option.label}&nbsp;</Typography>
            </Grid>
            <Grid item xs={2}>
              <Typography variant="caption" color={priorityColor(option.priority)}>
                {option.priority.label}
              </Typography>
            </Grid>
          </Grid>
        );
      }}
      renderInput={(params) => (
        <TextField
          required
          variant="standard"
          {...params}
          label={label}
          inputProps={{
            ...params.inputProps,
            autoComplete: "new-password", // disable autocomplete and autofill
          }}
          value={selected}
          error={error}
          helperText={helperText}
        />
      )}
    />
  );
};

export default CategoryAutoComplete;
