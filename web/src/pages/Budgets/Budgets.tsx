import { useCallback, useEffect, useMemo, useState } from "react";
import dayjs from "dayjs";
import {
  Button,
  Caption1,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { AddRegular, DeleteRegular, EditRegular } from "@fluentui/react-icons";
import BudgetForm from "../../components/BudgetForm/BudgetForm";
import BudgetMeter from "../../components/BudgetMeter/BudgetMeter";
import { Recurrence } from "../../clients/types";
import { budgetFormCategoryAdapter, type BudgetFormCategoryAdapter } from "../../budgets/budgetFormAdapter";
import { BudgetFormValues, toCreateRequest } from "../../budgets/budgetFormRules";
import { toMajorUnits } from "../../money/formatMoney";
import { Currency } from "../../typings/enums/Currency";
import { useVault } from "../../vault/VaultProvider";
import type { BudgetView, TaxonomyValue } from "../../vault/VaultProjection";

const dayFormat = "YYYY-MM-DD";

const useStyles = makeStyles({
  actions: {
    display: "flex",
    justifyContent: "flex-end",
    marginBlockEnd: tokens.spacingVerticalL,
  },
  grid: {
    display: "grid",
    gap: tokens.spacingHorizontalL,
    gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))",
  },
  empty: {
    color: tokens.colorNeutralForeground2,
  },
});

const valuesForNewBudget = (): BudgetFormValues => ({
  categoryId: null,
  amountMajorUnits: "",
  currency: Currency.EUR,
  recurrence: "Monthly",
  startsOn: dayjs().format(dayFormat),
  endsOn: null,
  alertThresholdPercent: null,
});

const valuesForExistingBudget = (
  budget: BudgetView,
  categories: BudgetFormCategoryAdapter,
): BudgetFormValues => ({
  categoryId: categories.tokenFor(budget.category.id),
  amountMajorUnits: String(toMajorUnits(budget.amount)),
  currency: budget.amount.currency,
  recurrence: budget.recurrence as Recurrence,
  startsOn: budget.startsOn,
  endsOn: budget.endsOn,
  alertThresholdPercent: budget.alertThresholdPercent,
});

const Budgets = () => {
  const styles = useStyles();
  const { projection, state } = useVault();
  const [budgets, setBudgets] = useState<BudgetView[]>([]);
  const [categories, setCategories] = useState<TaxonomyValue[]>([]);
  const [editing, setEditing] = useState<BudgetView | "new" | null>(null);
  const [pendingDelete, setPendingDelete] = useState<BudgetView | null>(null);
  const now = useMemo(() => dayjs(), []);
  const categoryAdapter = useMemo(() => budgetFormCategoryAdapter(categories), [categories]);

  const refreshBudgets = useCallback(() => {
    if (state !== "unlocked") {
      setBudgets([]);
      return Promise.resolve();
    }
    return projection.listBudgets("personal", now.toDate()).then(setBudgets);
  }, [now, projection, state]);

  useEffect(() => {
    refreshBudgets();
    if (state !== "unlocked") {
      setCategories([]);
      return;
    }
    projection
      .listTaxonomy("personal", "category")
      .then(setCategories)
      .catch(() => setCategories([]));
  }, [projection, refreshBudgets, state]);

  const handleSubmit = (values: BudgetFormValues) => {
    if (editing === null) return;
    const request = toCreateRequest(values);
    const saved = projection.saveBudget("personal", {
      id: editing === "new" ? null : editing.id,
      categoryId: categoryAdapter.recordIdFor(request.categoryId),
      amount: request.amount,
      recurrence: request.recurrence,
      startsOn: request.startsOn,
      endsOn: request.endsOn,
      alertThresholdPercent: request.alertThresholdPercent,
    });
    saved.then(refreshBudgets).then(() => setEditing(null));
  };

  const handleDelete = () => {
    if (pendingDelete === null) return;
    projection
      .deleteBudget("personal", pendingDelete.id)
      .then(refreshBudgets)
      .then(() => setPendingDelete(null));
  };

  return (
    <>
      <div className={styles.actions}>
        <Button appearance="primary" icon={<AddRegular />} onClick={() => setEditing("new")}>
          New budget
        </Button>
      </div>

      {budgets.length === 0 ? (
        <Caption1 className={styles.empty}>No budgets yet.</Caption1>
      ) : (
        <div className={styles.grid}>
          {budgets.map((budget) => (
            <BudgetMeter
              key={budget.id}
              budget={budget}
              now={now}
              actions={
                budget.canEdit ? <>
                  <Button
                    size="small"
                    appearance="subtle"
                    icon={<EditRegular />}
                    aria-label={`Edit the ${budget.category.label} budget`}
                    onClick={() => setEditing(budget)}>
                    Edit
                  </Button>
                  <Button
                    size="small"
                    appearance="subtle"
                    icon={<DeleteRegular />}
                    aria-label={`Delete the ${budget.category.label} budget`}
                    onClick={() => setPendingDelete(budget)}>
                    Delete
                  </Button>
                </> : undefined
              }
            />
          ))}
        </div>
      )}

      <Dialog
        open={editing !== null}
        onOpenChange={(_event, data) => !data.open && setEditing(null)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>{editing === "new" ? "New budget" : "Edit budget"}</DialogTitle>
            <DialogContent>
              {editing !== null && (
                <BudgetForm
                  categories={categoryAdapter.options}
                  initialValues={
                    editing === "new"
                      ? valuesForNewBudget()
                      : valuesForExistingBudget(editing, categoryAdapter)
                  }
                  onSubmit={handleSubmit}
                  onCancel={() => setEditing(null)}
                  submitLabel={editing === "new" ? "Create" : "Save"}
                  isEditing={editing !== "new"}
                />
              )}
            </DialogContent>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      <Dialog
        open={pendingDelete !== null}
        onOpenChange={(_event, data) => !data.open && setPendingDelete(null)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Delete this budget?</DialogTitle>
            <DialogContent>
              The {pendingDelete?.category.label} budget will be removed. The transactions it
              tracked are not deleted with it.
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setPendingDelete(null)}>
                Cancel
              </Button>
              <Button appearance="primary" onClick={handleDelete}>
                Delete
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </>
  );
};

export default Budgets;
