import type { ICategoryResponse } from "../clients/types";
import type { RecordId, TaxonomyValue } from "../vault/VaultProjection";

const invalidCategoryMessage = "The budget must have a valid category selection.";

export interface BudgetFormCategoryAdapter {
  options: ICategoryResponse[];
  tokenFor(recordId: RecordId): number;
  recordIdFor(token: number | null): RecordId;
}

export const budgetFormCategoryAdapter = (
  categories: readonly TaxonomyValue[],
): BudgetFormCategoryAdapter => {
  const recordIdsByToken = new Map<number, RecordId>();
  const tokensByRecordId = new Map<RecordId, number>();
  const options = categories.map((category, index) => {
    const token = index + 1;
    recordIdsByToken.set(token, category.id);
    tokensByRecordId.set(category.id, token);
    return {
      id: token,
      label: category.label,
      priority: {
        id: 0,
        label: category.priority ?? "Useful",
        weight: 0,
        createdAt: "",
        updatedAt: null,
      },
      createdAt: "",
      updatedAt: null,
    };
  });

  return {
    options,
    tokenFor(recordId) {
      const token = tokensByRecordId.get(recordId);
      if (token === undefined) throw new Error(invalidCategoryMessage);
      return token;
    },
    recordIdFor(token) {
      if (token === null || !Number.isSafeInteger(token)) throw new Error(invalidCategoryMessage);
      const recordId = recordIdsByToken.get(token);
      if (recordId === undefined) throw new Error(invalidCategoryMessage);
      return recordId;
    },
  };
};
