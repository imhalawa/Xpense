import { useEffect, useState } from "react";
import { Badge, Combobox, Field, Option, makeStyles, tokens } from "@fluentui/react-components";
import { ICategory } from "../../../../typings";
import { listCategories } from "../../../../clients/options";
import { useLoading } from "../../../../contexts/LoadingContext";

interface ICategoryAutoCompleteProps {
  label: string;
  value: ICategory | null;
  error?: boolean;
  helperText?: string;
  onChange: (value: ICategory | null) => void;
}

const useStyles = makeStyles({
  option: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    width: "100%",
    gap: tokens.spacingHorizontalM,
  },
});

const CategoryAutoComplete = ({
  label,
  value,
  error,
  helperText,
  onChange,
}: ICategoryAutoCompleteProps) => {
  const styles = useStyles();
  const { setLoading } = useLoading();
  const [categoryOptions, setCategoryOptions] = useState<ICategory[]>([]);
  const [selected, setSelected] = useState<ICategory | null>(value);

  useEffect(() => {
    setLoading(true);
    listCategories()
      .then((categories) => {
        setCategoryOptions(categories);
        setSelected(
          value ??
            [...categories].sort(
              (left, right) => right.priority.weight - left.priority.weight
            )[0] ??
            null
        );
        setLoading(false);
      })
      .catch((loadError) => {
        console.error(loadError);
        setLoading(false);
      });
  }, []);

  useEffect(() => {
    onChange(selected);
  }, [selected]);

  return (
    <Field
      label={label}
      required
      validationState={error ? "error" : "none"}
      validationMessage={helperText}>
      <Combobox
        value={selected?.label ?? ""}
        selectedOptions={selected?.id === null || selected === null ? [] : [String(selected.id)]}
        onOptionSelect={(_event, data) =>
          setSelected(
            categoryOptions.find((category) => String(category.id) === data.optionValue) ?? null
          )
        }>
        {categoryOptions.map((category) => (
          <Option key={category.id} value={String(category.id)} text={category.label}>
            <span className={styles.option}>
              {category.label}
              <Badge appearance="tint">{category.priority.label}</Badge>
            </span>
          </Option>
        ))}
      </Combobox>
    </Field>
  );
};

export default CategoryAutoComplete;
