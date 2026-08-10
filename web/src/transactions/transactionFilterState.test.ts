import { describe, expect, it } from "vitest";
import type { TransactionFilter } from "../vault/VaultProjection";
import {
  clearTransactionFilters,
  countActiveFilters,
  localPageBounds,
  parseTransactionFilter,
  serialiseTransactionFilter,
  stripFacets,
  toggleTaxonomyFilter,
} from "./transactionFilterState";

const emptyFilter: TransactionFilter = {
  space: "personal",
  category: null,
  merchant: null,
  tag: null,
  account: null,
  from: null,
  to: null,
};

describe("parseTransactionFilter", () => {
  it("falls back to the caller's active space when the URL carries none", () => {
    const filter = parseTransactionFilter(new URLSearchParams(""), "personal");

    expect(filter).toEqual(emptyFilter);
  });

  it("takes the space from the URL when it is there", () => {
    const filter = parseTransactionFilter(new URLSearchParams("space=household"), "personal");

    expect(filter.space).toBe("household");
  });

  it("parses each facet on its own", () => {
    const category = parseTransactionFilter(new URLSearchParams("category=3"), "personal");
    const merchant = parseTransactionFilter(new URLSearchParams("merchant=7"), "personal");
    const tag = parseTransactionFilter(new URLSearchParams("tag=11"), "personal");
    const account = parseTransactionFilter(new URLSearchParams("account=NL01"), "personal");

    expect(category).toEqual({ ...emptyFilter, category: "3" });
    expect(merchant).toEqual({ ...emptyFilter, merchant: "7" });
    expect(tag).toEqual({ ...emptyFilter, tag: "11" });
    expect(account).toEqual({ ...emptyFilter, account: "NL01" });
  });

  it("keeps both facets when two are set at once", () => {
    const filter = parseTransactionFilter(new URLSearchParams("category=3&tag=11"), "personal");

    expect(filter.category).toBe("3");
    expect(filter.tag).toBe("11");
  });

  it("keeps the first value of a repeated parameter and drops the rest", () => {
    const filter = parseTransactionFilter(new URLSearchParams("category=3&category=9"), "personal");

    expect(filter.category).toBe("3");
  });

  it("reads an empty parameter as no filter at all", () => {
    const filter = parseTransactionFilter(new URLSearchParams("category=&space="), "personal");

    expect(filter.category).toBeNull();
    expect(filter.space).toBe("personal");
  });

  it("accepts a well formed calendar date range", () => {
    const filter = parseTransactionFilter(
      new URLSearchParams("from=2026-01-01&to=2026-01-31"),
      "personal",
    );

    expect(filter.from).toBe("2026-01-01");
    expect(filter.to).toBe("2026-01-31");
  });

  it("rejects a date that is not a real calendar day", () => {
    const impossibleMonthAndDay = parseTransactionFilter(
      new URLSearchParams("from=2026-13-40"),
      "personal",
    );
    const wrongShape = parseTransactionFilter(new URLSearchParams("to=31-01-2026"), "personal");
    const notALeapYear = parseTransactionFilter(new URLSearchParams("from=2026-02-29"), "personal");

    expect(impossibleMonthAndDay.from).toBeNull();
    expect(wrongShape.to).toBeNull();
    expect(notALeapYear.from).toBeNull();
  });

  it("clears both ends when from is later than to", () => {
    const filter = parseTransactionFilter(
      new URLSearchParams("from=2026-03-01&to=2026-02-01&category=3"),
      "personal",
    );

    expect(filter.from).toBeNull();
    expect(filter.to).toBeNull();
    expect(filter.category).toBe("3");
  });

  it("keeps a range whose ends are the same day", () => {
    const filter = parseTransactionFilter(
      new URLSearchParams("from=2026-03-01&to=2026-03-01"),
      "personal",
    );

    expect(filter.from).toBe("2026-03-01");
    expect(filter.to).toBe("2026-03-01");
  });
});

describe("serialiseTransactionFilter", () => {
  it("omits every null facet rather than writing an empty value", () => {
    const search = serialiseTransactionFilter({ ...emptyFilter, category: "3" });

    expect(search.toString()).toBe("space=personal&category=3");
  });

  it("round-trips a fully populated filter without drift", () => {
    const filter: TransactionFilter = {
      space: "household",
      category: "3",
      merchant: "7",
      tag: "11",
      account: "NL01",
      from: "2026-01-01",
      to: "2026-01-31",
    };

    const once = parseTransactionFilter(serialiseTransactionFilter(filter), "personal");
    const twice = parseTransactionFilter(serialiseTransactionFilter(once), "personal");

    expect(once).toEqual(filter);
    expect(twice).toEqual(filter);
    expect(serialiseTransactionFilter(twice).toString()).toBe(
      serialiseTransactionFilter(filter).toString(),
    );
  });
});

describe("toggleTaxonomyFilter", () => {
  it("sets the clicked facet and leaves every other facet alone", () => {
    const filter: TransactionFilter = {
      ...emptyFilter,
      merchant: "7",
      account: "NL01",
      from: "2026-01-01",
      to: "2026-01-31",
    };

    const toggled = toggleTaxonomyFilter(filter, "category", "3");

    expect(toggled).toEqual({ ...filter, category: "3" });
  });

  it("clears the facet when the selected value is clicked again", () => {
    const filter: TransactionFilter = {
      ...emptyFilter,
      category: "3",
      merchant: "7",
      account: "NL01",
    };

    const toggled = toggleTaxonomyFilter(filter, "category", "3");

    expect(toggled.category).toBeNull();
    expect(toggled.merchant).toBe("7");
    expect(toggled.account).toBe("NL01");
  });

  it("replaces a different value in the same facet", () => {
    const toggled = toggleTaxonomyFilter({ ...emptyFilter, tag: "11" }, "tag", "12");

    expect(toggled.tag).toBe("12");
  });

  it("does not mutate the filter it was given", () => {
    const filter: TransactionFilter = { ...emptyFilter, category: "3" };

    toggleTaxonomyFilter(filter, "category", "9");

    expect(filter.category).toBe("3");
  });
});

describe("clearTransactionFilters", () => {
  it("keeps the space and nulls everything else", () => {
    const filter: TransactionFilter = {
      space: "household",
      category: "3",
      merchant: "7",
      tag: "11",
      account: "NL01",
      from: "2026-01-01",
      to: "2026-01-31",
    };

    expect(clearTransactionFilters(filter)).toEqual({ ...emptyFilter, space: "household" });
  });
});

describe("countActiveFilters", () => {
  it("counts nothing when only the space is set", () => {
    expect(countActiveFilters(emptyFilter)).toBe(0);
  });

  it("counts a full date range as one filter", () => {
    expect(
      countActiveFilters({ ...emptyFilter, from: "2026-01-01", to: "2026-01-31" }),
    ).toBe(1);
  });

  it("counts a half-open date range as one filter", () => {
    expect(countActiveFilters({ ...emptyFilter, from: "2026-01-01" })).toBe(1);
    expect(countActiveFilters({ ...emptyFilter, to: "2026-01-31" })).toBe(1);
  });

  it("counts a category plus a date range as two", () => {
    expect(
      countActiveFilters({
        ...emptyFilter,
        category: "3",
        from: "2026-01-01",
        to: "2026-01-31",
      }),
    ).toBe(2);
  });

  it("counts every taxonomy facet and the account separately", () => {
    expect(
      countActiveFilters({
        ...emptyFilter,
        category: "3",
        merchant: "7",
        tag: "11",
        account: "NL01",
      }),
    ).toBe(4);
  });
});

describe("stripFacets", () => {
  it("nulls the named facets and leaves the rest standing", () => {
    const filter: TransactionFilter = {
      space: "household",
      category: "3",
      merchant: "7",
      tag: "11",
      account: "NL01",
      from: "2026-01-01",
      to: "2026-01-31",
    };

    const stripped = stripFacets(filter, ["category", "account", "from"]);

    expect(stripped).toEqual({
      space: "household",
      category: null,
      merchant: "7",
      tag: "11",
      account: null,
      from: null,
      to: "2026-01-31",
    });
  });

  it("returns an equal filter when nothing was removed", () => {
    const filter: TransactionFilter = { ...emptyFilter, category: "3" };

    expect(stripFacets(filter, [])).toEqual(filter);
  });

  it("never strips the space, because a filter without one cannot exist", () => {
    const stripped = stripFacets({ ...emptyFilter, space: "household" }, ["space"]);

    expect(stripped.space).toBe("household");
  });
});

describe("localPageBounds", () => {
  it("hands back a full page of fifty rows for the first page", () => {
    expect(localPageBounds(120, 0)).toEqual({ offset: 0, limit: 50 });
  });

  it("clamps the last page to the rows that are left", () => {
    expect(localPageBounds(120, 2)).toEqual({ offset: 100, limit: 20 });
  });

  it("hands back an empty page past the end", () => {
    expect(localPageBounds(120, 5)).toEqual({ offset: 250, limit: 0 });
  });

  it("hands back an empty page when there are no rows at all", () => {
    expect(localPageBounds(0, 0)).toEqual({ offset: 0, limit: 0 });
  });
});
