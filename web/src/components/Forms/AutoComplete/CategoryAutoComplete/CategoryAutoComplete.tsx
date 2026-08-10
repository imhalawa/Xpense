import { useEffect, useRef, useState } from "react";
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

const mostEssential = (categories: ICategory[]): ICategory | null =>
  [...categories].sort((left, right) => right.priority.weight - left.priority.weight)[0] ?? null;

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
  const [query, setQuery] = useState<string | null>(null);
  const hasProposedDefault = useRef(false);

  useEffect(() => {
    let stale = false;
    setLoading(true);
    listCategories()
      .then((categories) => {
        if (stale) return;
        setCategoryOptions(categories);
        setLoading(false);
      })
      .catch((loadError) => {
        if (stale) return;
        console.error(loadError);
        setLoading(false);
      });

    return () => {
      stale = true;
    };
  }, [setLoading]);

  useEffect(() => {
    if (hasProposedDefault.current || value !== null || categoryOptions.length === 0) return;
    const preferred = mostEssential(categoryOptions);
    if (preferred === null) return;
    hasProposedDefault.current = true;
    onChange(preferred);
  }, [categoryOptions, onChange, value]);

  const matches = query === null
    ? categoryOptions
    : categoryOptions.filter((category) =>
        category.label.toLowerCase().includes(query.toLowerCase()));

  return (
    <Field
      label={label}
      required
      validationState={error ? "error" : "none"}
      validationMessage={helperText}>
      <Combobox
        freeform
        value={query ?? value?.label ?? ""}
        selectedOptions={value === null || value.id === null ? [] : [String(value.id)]}
        onChange={(event) => setQuery(event.target.value)}
        onBlur={() => setQuery(null)}
        onOptionSelect={(_event, data) => {
          setQuery(null);
          onChange(
            categoryOptions.find((category) => String(category.id) === data.optionValue) ?? null,
          );
        }}>
        {matches.map((category) => (
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
