import { Autocomplete, Grid, TextField, Typography } from "@mui/material";
import { ICategory, IPriority } from "../../../../typings/models/ICategory";
import { createCategoriesFixture } from "../../../../fixtures";
import { useEffect, useState } from "react";

interface ICategoryAutoCompleteProps {
  label: string;
  value: ICategory | null;
  error?: boolean;
  helperText?: string;
  onChange: (value: ICategory | null) => void;
}
const CategoryAutoComplete = ({ label, value, error, helperText, onChange }: ICategoryAutoCompleteProps) => {
  const categories = createCategoriesFixture();
  const defaultCategory = value || (categories.find((c) => c.priority.weight === 2) ?? null);
  const [selected, setSelected] = useState<ICategory | null>(defaultCategory);

  // Later, give the user the option to pick a color for the priority
  const priorityColor = (priority: IPriority): string => {
    switch (priority.weight) {
      case 1:
        return "green";
      case 2:
        return "orange";
      case 3:
        return "red";
      default:
        throw new Error(`Invalid priority value: ${priority.weight}`);
    }
  };

  useEffect(() => {
    onChange(selected);
  }, [selected]);

  return (
    <Autocomplete
      id="category-autocomplete"
      options={categories}
      isOptionEqualToValue={(a, b) => a.id === b.id}
      autoHighlight
      value={defaultCategory}
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
                {option.priority.name}
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
