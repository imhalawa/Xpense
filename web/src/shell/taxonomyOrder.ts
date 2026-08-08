import type { RecordId, TaxonomyKind, TaxonomyValue } from "../vault/VaultProjection";

const maximumRecentValues = 20;
const storageKeyPrefix = "xpense.sidebar.recent";

const storageKey = (kind: TaxonomyKind) => `${storageKeyPrefix}.${kind}`;

export const readTaxonomyRecency = (kind: TaxonomyKind): RecordId[] => {
  try {
    const stored = window.localStorage.getItem(storageKey(kind));
    if (stored === null) return [];

    const parsed: unknown = JSON.parse(stored);
    if (!Array.isArray(parsed)) return [];

    return parsed.filter((entry): entry is string => typeof entry === "string").slice(0, maximumRecentValues);
  } catch {
    return [];
  }
};

export const rememberTaxonomyUse = (kind: TaxonomyKind, id: RecordId): void => {
  const recent = [id, ...readTaxonomyRecency(kind).filter((recentId) => recentId !== id)].slice(
    0,
    maximumRecentValues,
  );

  try {
    window.localStorage.setItem(storageKey(kind), JSON.stringify(recent));
  } catch {
    return;
  }
};

export const orderTaxonomyValues = (
  values: TaxonomyValue[],
  recentIds: RecordId[],
): TaxonomyValue[] => {
  const valuesById = new Map(values.map((value) => [value.id, value]));
  const recent = recentIds
    .map((id) => valuesById.get(id))
    .filter((value): value is TaxonomyValue => value !== undefined);
  const recentSet = new Set(recent.map((value) => value.id));
  const alphabetical = values
    .filter((value) => !recentSet.has(value.id))
    .sort((left, right) => left.label.localeCompare(right.label, undefined, { sensitivity: "base" }));

  return [...recent, ...alphabetical];
};
