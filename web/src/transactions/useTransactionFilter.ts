import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate, useSearchParams } from "react-router";
import type {
  FilterFacet,
  RecordId,
  SpaceId,
  TaxonomyKind,
  TransactionFilter,
  VaultProjection,
} from "../vault/VaultProjection";
import {
  clearTransactionFilters,
  countActiveFilters,
  parseTransactionFilter,
  serialiseTransactionFilter,
  stripFacets,
  toggleTaxonomyFilter,
} from "./transactionFilterState";

type StrippableFacet = Exclude<FilterFacet, "space">;

const FACET_LABELS: Record<StrippableFacet, string> = {
  category: "category",
  merchant: "merchant",
  tag: "tag",
  account: "account",
  from: "start date",
  to: "end date",
};

const isStrippable = (facet: FilterFacet): facet is StrippableFacet => facet !== "space";

const describeRemoval = (filter: TransactionFilter, removed: StrippableFacet[]): string =>
  removed.map((facet) => `${facet}=${filter[facet] ?? ""}`).join("&");

const announceRemoval = (removed: StrippableFacet[]): string => {
  const labels = removed.map((facet) => FACET_LABELS[facet]);

  if (labels.length === 1) {
    return `The ${labels[0]} filter was removed because it is no longer available.`;
  }

  const leading = labels.slice(0, -1).join(", ");
  const last = labels[labels.length - 1];

  return `The ${leading} and ${last} filters were removed because they are no longer available.`;
};

export function useTransactionFilter(projection: VaultProjection, fallbackSpace: SpaceId) {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const [removedMessage, setRemovedMessage] = useState<string | null>(null);
  const announcedRemoval = useRef<string | null>(null);
  const searchKey = searchParams.toString();

  const filterFromUrl = useMemo(
    () => parseTransactionFilter(new URLSearchParams(searchKey), fallbackSpace),
    [searchKey, fallbackSpace],
  );

  const [filter, setResolvedFilter] = useState<TransactionFilter>(filterFromUrl);

  useEffect(() => {
    let isCurrentResolution = true;

    const applyResolution = (removed: FilterFacet[], resolved: TransactionFilter) => {
      if (!isCurrentResolution) {
        return;
      }

      const strippable = removed.filter(isStrippable);
      if (strippable.length === 0) {
        setResolvedFilter(resolved);
        return;
      }

      const stripped = stripFacets(filterFromUrl, strippable);
      const removalKey = describeRemoval(filterFromUrl, strippable);

      if (announcedRemoval.current !== removalKey) {
        announcedRemoval.current = removalKey;
        setRemovedMessage(announceRemoval(strippable));
      }

      setResolvedFilter(stripped);
      navigate({ search: serialiseTransactionFilter(stripped).toString() }, { replace: true });
    };

    const keepFilterFromUrl = () => {
      if (isCurrentResolution) {
        setResolvedFilter(filterFromUrl);
      }
    };

    projection
      .resolveFilter(filterFromUrl)
      .then((resolution) => applyResolution(resolution.removed, resolution.filter))
      .catch(keepFilterFromUrl);

    return () => {
      isCurrentResolution = false;
    };
  }, [projection, filterFromUrl, navigate]);

  const setFilter = useCallback(
    (next: TransactionFilter) => {
      setResolvedFilter(next);
      navigate({ search: serialiseTransactionFilter(next).toString() });
    },
    [navigate],
  );

  const toggleTaxonomy = useCallback(
    (kind: TaxonomyKind, id: RecordId) => setFilter(toggleTaxonomyFilter(filter, kind, id)),
    [filter, setFilter],
  );

  const clearFilters = useCallback(
    () => setFilter(clearTransactionFilters(filter)),
    [filter, setFilter],
  );

  const dismissRemovedMessage = useCallback(() => setRemovedMessage(null), []);

  return {
    filter,
    setFilter,
    toggleTaxonomy,
    clearFilters,
    activeFilterCount: countActiveFilters(filter),
    removedMessage,
    dismissRemovedMessage,
  };
}
