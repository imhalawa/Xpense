import { Autocomplete, Grid, TextField, Typography } from "@mui/material";
import { ICategory, IPriority } from "../../../../typings/ICategory";

interface ICategoryAutoCompleteProps {
  options: ICategory[];
  label: string;
  defaultValue: ICategory | null;
  onChange: (value: ICategory | null) => void;
}
const CategoryAutoComplete = ({
  defaultValue,
  options,
  label,
  onChange,
}: ICategoryAutoCompleteProps) => {
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

  return (
    <Autocomplete
      id="category-autocomplete"
      options={options}
      isOptionEqualToValue={(a, b) => a.id === b.id}
      autoHighlight
      value={defaultValue}
      onChange={(event: any, newValue: ICategory | null) => {
        event.preventDefault();
        onChange(newValue);
      }}
      getOptionLabel={(option) => option.label}
      renderOption={(props, option) => {
        const { key, ...optionProps } = props;
        return (
          <Grid container spacing={1} key={key} component="li" {...optionProps}>
            <Grid item xs={10}>
              <Typography variant="body2">{option.label}&nbsp;</Typography>
            </Grid>
            <Grid item xs={2}>
              <Typography
                variant="caption"
                color={priorityColor(option.priority)}
              >
                {option.priority.name}
              </Typography>
            </Grid>
          </Grid>
        );
      }}
      renderInput={(params) => (
        <TextField
          required
          {...params}
          label={label}
          inputProps={{
            ...params.inputProps,
            autoComplete: "new-password", // disable autocomplete and autofill
          }}
        />
      )}
    />
  );
};

export default CategoryAutoComplete;
