import { useEffect, useState } from "react";
import { Badge, Combobox, Field, Option, makeStyles, tokens } from "@fluentui/react-components";
import { ITag } from "../../../../typings/models/ITag";
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

const searchDelayMilliseconds = 250;

const useStyles = makeStyles({
  selected: {
    display: "flex",
    flexWrap: "wrap",
    gap: tokens.spacingHorizontalXS,
    marginTop: tokens.spacingVerticalXS,
  },
});

const createdTag = (label: string): ITag => ({
  id: null,
  label,
  create: true,
  createdAt: null,
  updatedAt: null,
  fgColorHex: "",
  bgColorHex: "",
});

const TagAutoComplete = ({
  label,
  value,
  onChange,
  error,
  helperText,
}: ITagAutoCompleteProps) => {
  const styles = useStyles();
  const { setLoading } = useLoading();
  const [tagOptions, setTagOptions] = useState<ITag[]>([]);
  const [selected, setSelected] = useState<ITag[]>(value ?? []);
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
      .catch((loadError) => {
        if (stale) return;
        console.error(loadError);
        setLoading(false);
      });

    return () => {
      stale = true;
    };
  }, [searching, debouncedSearch]);

  const hasExactMatch = tagOptions.some((option) => option.label === search);
  const selectedValues = selected.map((tag) => (tag.id === null ? tag.label : String(tag.id)));

  const selectTag = (optionValue: string | undefined) => {
    if (optionValue === undefined) return;
    const existing = tagOptions.find((option) => String(option.id) === optionValue);
    const candidate = existing ?? createdTag(optionValue);
    const candidateValue = candidate.id === null ? candidate.label : String(candidate.id);
    const alreadySelected = selectedValues.includes(candidateValue);
    setSelected(
      alreadySelected
        ? selected.filter((tag) => (tag.id === null ? tag.label : String(tag.id)) !== candidateValue)
        : [...selected, candidate]
    );
    setSearch("");
  };

  return (
    <Field
      label={label}
      validationState={error ? "error" : "none"}
      validationMessage={helperText}>
      <Combobox
        multiselect
        freeform
        value={search}
        selectedOptions={selectedValues}
        onFocus={() => setSearching(true)}
        onChange={(event) => {
          setSearching(true);
          setSearch(event.target.value);
        }}
        onOptionSelect={(_event, data) => selectTag(data.optionValue)}>
        {tagOptions.map((tag) => (
          <Option key={tag.id} value={String(tag.id)} text={tag.label}>
            {tag.label}
          </Option>
        ))}
        {search !== "" && !hasExactMatch && (
          <Option value={search} text={`Create ${search}`}>
            Create “{search}”
          </Option>
        )}
      </Combobox>
      {selected.length > 0 && (
        <div className={styles.selected} aria-label="Selected tags">
          {selected.map((tag) => (
            <Badge key={tag.id ?? tag.label} appearance="tint" color="brand">
              {tag.label}
            </Badge>
          ))}
        </div>
      )}
    </Field>
  );
};

export default TagAutoComplete;
