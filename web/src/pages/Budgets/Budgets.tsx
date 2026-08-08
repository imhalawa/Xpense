import { useCallback, useEffect, useState } from "react";
import dayjs from "dayjs";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Dialog from "@mui/material/Dialog";
import DialogActions from "@mui/material/DialogActions";
import DialogContent from "@mui/material/DialogContent";
import DialogContentText from "@mui/material/DialogContentText";
import DialogTitle from "@mui/material/DialogTitle";
import Grid from "@mui/material/Grid";
import Typography from "@mui/material/Typography";
import BudgetForm from "../../components/BudgetForm/BudgetForm";
import BudgetMeter from "../../components/BudgetMeter/BudgetMeter";
import { createBudget, deleteBudget, listBudgets, updateBudget } from "../../clients/budgets";
import { listCategories } from "../../clients/options";
import { IBudgetResponse, ICategoryResponse, Recurrence } from "../../clients/types";
import { BudgetFormValues, toCreateRequest } from "../../budgets/budgetFormRules";
import { toMajorUnits } from "../../money/formatMoney";
import { Currency } from "../../typings/enums/Currency";
import PageHeader from "../../shell/PageHeader";

const dayFormat = "YYYY-MM-DD";

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
      <PageHeader
        title="Budgets"
        actions={
          <Button variant="contained" onClick={() => setEditing("new")}>
            New budget
          </Button>
        }
      />
      <Grid size={12}>
        <Box sx={{ display: "flex", flexDirection: "column", gap: 2, width: "100%" }}>
          {budgets.length === 0 ? (
            <Typography variant="body2" color="text.secondary">
              No budgets yet.
            </Typography>
          ) : (
            <Box
              sx={{
                display: "grid",
                gap: 2,
                gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))",
              }}>
              {budgets.map((budget) => (
                <BudgetMeter
                  key={budget.id}
                  budget={budget}
                  now={now}
                  actions={
                    <>
                      <Button
                        size="small"
                        aria-label={`Edit the ${budget.category.label} budget`}
                        onClick={() => setEditing(budget)}>
                        Edit
                      </Button>
                      <Button
                        size="small"
                        color="error"
                        aria-label={`Delete the ${budget.category.label} budget`}
                        onClick={() => setPendingDelete(budget)}>
                        Delete
                      </Button>
                    </>
                  }
                />
              ))}
            </Box>
          )}
        </Box>
      </Grid>

      <Dialog open={editing !== null} onClose={() => setEditing(null)} fullWidth maxWidth="sm">
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
      </Dialog>

      <Dialog open={pendingDelete !== null} onClose={() => setPendingDelete(null)}>
        <DialogTitle>Delete this budget?</DialogTitle>
        <DialogContent>
          <DialogContentText>
            The {pendingDelete?.category.label} budget will be removed. The transactions it tracked
            are not deleted with it.
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPendingDelete(null)}>Cancel</Button>
          <Button variant="contained" color="error" onClick={handleDelete}>
            Delete
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
};

export default Budgets;
