import { beforeEach, describe, expect, it } from "vitest";
import type { TaxonomyValue } from "../vault/VaultProjection";
import {
  orderTaxonomyValues,
  readTaxonomyRecency,
  rememberTaxonomyUse,
} from "./taxonomyOrder";

const value = (id: string, label: string): TaxonomyValue => ({
  id,
  label,
  kind: "category",
  foregroundHex: null,
  backgroundHex: null,
});

describe("taxonomyOrder", () => {
  beforeEach(() => window.localStorage.clear());

  it("puts known recent values first and sorts the rest alphabetically", () => {
    const values = [
      value("1", "Zulu"),
      value("2", "Bravo"),
      value("3", "Alpha"),
      value("7", "Golf"),
    ];

    expect(orderTaxonomyValues(values, ["7", "2"]).map((entry) => entry.id)).toEqual([
      "7",
      "2",
      "3",
      "1",
    ]);
  });

  it("ignores recency ids that are absent", () => {
    const values = [value("2", "Bravo"), value("3", "Alpha")];

    expect(orderTaxonomyValues(values, ["99"]).map((entry) => entry.id)).toEqual(["3", "2"]);
  });

  it("caps stored recency at twenty and moves a repeated id to the front", () => {
    for (let index = 0; index < 22; index += 1) {
      rememberTaxonomyUse("tag", String(index));
    }

    expect(readTaxonomyRecency("tag")).toHaveLength(20);

    rememberTaxonomyUse("tag", "7");

    const recent = readTaxonomyRecency("tag");
    expect(recent[0]).toBe("7");
    expect(recent.filter((id) => id === "7")).toHaveLength(1);
  });
});
