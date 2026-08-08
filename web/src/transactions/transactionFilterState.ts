import type {
  FilterFacet,
  PageRequest,
  RecordId,
  SpaceId,
  TaxonomyKind,
  TransactionFilter,
} from "../vault/VaultProjection";

const CALENDAR_DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
const ROWS_PER_PAGE = 50;
const SPACE_PARAMETER = "space";
const RECORD_PARAMETERS = ["category", "merchant", "tag", "account"] as const;
const DATE_PARAMETERS = ["from", "to"] as const;

type RecordFacet = (typeof RECORD_PARAMETERS)[number];

function readRecordId(search: URLSearchParams, facet: RecordFacet): RecordId | null {
  const value = search.get(facet);
  return value === null || value === "" ? null : value;
}

function readCalendarDate(search: URLSearchParams, facet: (typeof DATE_PARAMETERS)[number]) {
  const value = search.get(facet);
  if (value === null || !CALENDAR_DATE_PATTERN.test(value)) {
    return null;
  }

  const [year, month, day] = value.split("-").map(Number);
  const candidate = new Date(Date.UTC(year, month - 1, day));
  const isRealCalendarDay =
    candidate.getUTCFullYear() === year &&
    candidate.getUTCMonth() === month - 1 &&
    candidate.getUTCDate() === day;

  return isRealCalendarDay ? value : null;
}

export function parseTransactionFilter(
  search: URLSearchParams,
  fallbackSpace: SpaceId,
): TransactionFilter {
  const space = search.get(SPACE_PARAMETER);
  const from = readCalendarDate(search, "from");
  const to = readCalendarDate(search, "to");
  const isBackwardsRange = from !== null && to !== null && from > to;

  return {
    space: space === null || space === "" ? fallbackSpace : space,
    category: readRecordId(search, "category"),
    merchant: readRecordId(search, "merchant"),
    tag: readRecordId(search, "tag"),
    account: readRecordId(search, "account"),
    from: isBackwardsRange ? null : from,
    to: isBackwardsRange ? null : to,
  };
}

export function serialiseTransactionFilter(filter: TransactionFilter): URLSearchParams {
  const search = new URLSearchParams();
  search.set(SPACE_PARAMETER, filter.space);

  for (const facet of [...RECORD_PARAMETERS, ...DATE_PARAMETERS]) {
    const value = filter[facet];
    if (value !== null) {
      search.set(facet, value);
    }
  }

  return search;
}

export function toggleTaxonomyFilter(
  filter: TransactionFilter,
  kind: TaxonomyKind,
  id: RecordId,
): TransactionFilter {
  return { ...filter, [kind]: filter[kind] === id ? null : id };
}

export function clearTransactionFilters(filter: TransactionFilter): TransactionFilter {
  return {
    space: filter.space,
    category: null,
    merchant: null,
    tag: null,
    account: null,
    from: null,
    to: null,
  };
}

export function countActiveFilters(filter: TransactionFilter): number {
  const setRecordFacets = RECORD_PARAMETERS.filter((facet) => filter[facet] !== null).length;
  const hasDateRange = filter.from !== null || filter.to !== null;

  return setRecordFacets + (hasDateRange ? 1 : 0);
}

export function stripFacets(
  filter: TransactionFilter,
  removed: FilterFacet[],
): TransactionFilter {
  const stripped = { ...filter };

  for (const facet of removed) {
    if (facet !== SPACE_PARAMETER) {
      stripped[facet] = null;
    }
  }

  return stripped;
}

export function localPageBounds(totalRows: number, pageIndex: number): PageRequest {
  const offset = pageIndex * ROWS_PER_PAGE;
  const remainingRows = Math.max(totalRows - offset, 0);

  return { offset, limit: Math.min(ROWS_PER_PAGE, remainingRows) };
}
