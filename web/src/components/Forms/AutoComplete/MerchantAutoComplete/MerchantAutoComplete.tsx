import { useEffect, useState } from "react";
import { Combobox, Field, Option } from "@fluentui/react-components";
import { IMerchant } from "../../../../typings";
import { listMerchants } from "../../../../clients/options";
import { useLoading } from "../../../../contexts/LoadingContext";
import { useDebouncedValue } from "../../../../hooks/useDebouncedValue";

interface IMerchantAutoCompleteProps {
  label: string;
  value: IMerchant | null;
  error?: boolean;
  helperText?: string;
  onChange: (value: IMerchant | null) => void;
}

const searchDelayMilliseconds = 250;

const MerchantAutoComplete = ({
  label,
  value,
  onChange,
  error,
  helperText,
}: IMerchantAutoCompleteProps) => {
  const { setLoading } = useLoading();
  const [merchantOptions, setMerchantOptions] = useState<IMerchant[]>([]);
  const [query, setQuery] = useState<string | null>(null);
  const [searching, setSearching] = useState(false);
  const search = query ?? value?.label ?? "";
  const debouncedSearch = useDebouncedValue(search, searchDelayMilliseconds);

  useEffect(() => {
    if (!searching) return;

    let stale = false;
    setLoading(true);
    listMerchants({ search: debouncedSearch })
      .then((merchants) => {
        if (stale) return;
        setMerchantOptions(merchants);
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

  const hasExactMatch = merchantOptions.some((option) => option.label === search);

  return (
    <Field
      label={label}
      required
      validationState={error ? "error" : "none"}
      validationMessage={helperText}>
      <Combobox
        freeform
        value={search}
        selectedOptions={value === null || value.id === null ? [] : [String(value.id)]}
        onFocus={() => setSearching(true)}
        onChange={(event) => {
          setSearching(true);
          setQuery(event.target.value);
        }}
        onOptionSelect={(_event, data) => {
          const existing = merchantOptions.find((option) => String(option.id) === data.optionValue);
          const next =
            existing ??
            (search === "" ? null : { id: null, label: search, create: true });
          setQuery(null);
          onChange(next);
        }}>
        {merchantOptions.map((merchant) => (
          <Option key={merchant.id} value={String(merchant.id)} text={merchant.label}>
            {merchant.label}
          </Option>
        ))}
        {search !== "" && !hasExactMatch && (
          <Option value={search} text={`Create ${search}`}>
            Create “{search}”
          </Option>
        )}
      </Combobox>
    </Field>
  );
};

export default MerchantAutoComplete;
