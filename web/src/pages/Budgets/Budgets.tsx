import { useCallback, useEffect, useState } from "react";
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
import { createBudget, deleteBudget, listBudgets, updateBudget } from "../../clients/budgets";
import { listCategories } from "../../clients/options";
import { IBudgetResponse, ICategoryResponse, Recurrence } from "../../clients/types";
import { BudgetFormValues, toCreateRequest } from "../../budgets/budgetFormRules";
import { toMajorUnits } from "../../money/formatMoney";
import { Currency } from "../../typings/enums/Currency";

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

const valuesForExistingBudget = (budget: IBudgetResponse): BudgetFormValues => ({
  categoryId: budget.category.id,
  amountMajorUnits: String(toMajorUnits(budget.amount)),
  currency: budget.amount.currency,
  recurrence: budget.recurrence as Recurrence,
  startsOn: budget.startsOn,
  endsOn: budget.endsOn,
  alertThresholdPercent: budget.alertThresholdPercent,
});

const Budgets = () => {
  const styles = useStyles();
  const [budgets, setBudgets] = useState<IBudgetResponse[]>([]);
  const [categories, setCategories] = useState<ICategoryResponse[]>([]);
  const [editing, setEditing] = useState<IBudgetResponse | "new" | null>(null);
  const [pendingDelete, setPendingDelete] = useState<IBudgetResponse | null>(null);
  const now = dayjs();

  const refreshBudgets = useCallback(() => listBudgets(dayjs()).then(setBudgets), []);

  useEffect(() => {
    refreshBudgets();
    listCategories().then(setCategories);
  }, [refreshBudgets]);

  const handleSubmit = (values: BudgetFormValues) => {
    if (editing === null) return;
    const request = toCreateRequest(values);
    const { categoryId, ...withoutCategory } = request;
    const saved =
      editing === "new" ? createBudget(request) : updateBudget(editing.id, withoutCategory);
    saved.then(refreshBudgets).then(() => setEditing(null));
  };

  const handleDelete = () => {
    if (pendingDelete === null) return;
    deleteBudget(pendingDelete.id)
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
                <>
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
                </>
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
                  categories={categories}
                  initialValues={
                    editing === "new" ? valuesForNewBudget() : valuesForExistingBudget(editing)
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
