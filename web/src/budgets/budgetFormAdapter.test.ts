import { describe, expect, it } from "vitest";
import { budgetFormCategoryAdapter } from "./budgetFormAdapter";

const categoryId = "11111111-1111-4111-8111-111111111111";

describe("budgetFormCategoryAdapter", () => {
  it("round-trips a UUID category through a finite form token", () => {
    const adapter = budgetFormCategoryAdapter([
      {
        id: categoryId,
        kind: "category",
        label: "Food",
        foregroundHex: null,
        backgroundHex: null,
      },
    ]);

    expect(adapter.options).toEqual([expect.objectContaining({ id: 1, label: "Food" })]);
    expect(adapter.tokenFor(categoryId)).toBe(1);
    expect(adapter.recordIdFor(1)).toBe(categoryId);
  });

  it.each([null, Number.NaN, Number.POSITIVE_INFINITY, 0, 2])(
    "rejects an invalid category token %s",
    (token) => {
      const adapter = budgetFormCategoryAdapter([
        {
          id: categoryId,
          kind: "category",
          label: "Food",
          foregroundHex: null,
          backgroundHex: null,
        },
      ]);

      expect(() => adapter.recordIdFor(token)).toThrow("valid category selection");
    },
  );
});
