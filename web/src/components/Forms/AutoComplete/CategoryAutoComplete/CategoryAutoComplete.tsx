import { Autocomplete, Grid, TextField, Typography } from "@mui/material";
import { useEffect, useState } from "react";
import { ICategory, IPriority } from "../../../../typings";
import { listCategories } from "../../../../clients/options";
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

  const priorityColor = (priority: IPriority): string => {
    switch (priority.weight) {
      case 1:
        return "success.main";
      case 2:
        return "warning.main";
      case 3:
        return "error.main";
      default:
        return "text.secondary";
    }
  };

  useEffect(() => {
    setLoading(true);
    listCategories()
      .then((categories) => {
        setCategoryOptions(categories);
        setSelected(
          value ||
            ([...categories].sort((left, right) => right.priority.weight - left.priority.weight)[0] ?? null)
        );
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
      id="category-autocomplete"
      options={categoryOptions}
      isOptionEqualToValue={(a, b) => a.id === b.id}
      autoHighlight
      value={selected}
      onChange={(_, newValue: ICategory | null) => setSelected(newValue)}
      getOptionLabel={(option) => option.label}
      renderOption={(props, option) => {
        const { key, ...optionProps } = props;
        return (
          <Grid container spacing={1} key={key} component="li" {...optionProps}>
            <Grid size={10}>
              <Typography variant="body2">{option.label}&nbsp;</Typography>
            </Grid>
            <Grid size={2}>
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
          {...params}
          label={label}
          value={selected}
          error={error}
          helperText={helperText}
          slotProps={{
            ...params.slotProps,

            htmlInput: {
              ...params.slotProps.htmlInput,
              autoComplete: "new-password",
            }
          }}
        />
      )}
    />
  );
};

export default CategoryAutoComplete;
