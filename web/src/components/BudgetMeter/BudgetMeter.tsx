import { Dayjs } from "dayjs";
import Box from "@mui/material/Box";
import LinearProgress from "@mui/material/LinearProgress";
import Paper from "@mui/material/Paper";
import Typography from "@mui/material/Typography";
import { IBudgetResponse } from "../../clients/types";
import { budgetProgress, BudgetState } from "../../budgets/budgetProgress";
import { formatMoney } from "../../money/formatMoney";
import { tokens } from "../../theme/tokens";
import DeltaChip from "../DeltaChip/DeltaChip";

interface BudgetMeterProps {
  budget: IBudgetResponse;
  now: Dayjs;
}

const barColour: Record<"light" | "dark", Record<BudgetState, string>> = {
  light: {
    "not-measuring": tokens.ink.light.muted,
    "on-track": tokens.brand[500],
    "projected-over": tokens.money.light.expenseOrdinary,
    "threshold-passed": tokens.money.light.expenseOrdinary,
    exceeded: tokens.money.light.expenseAlert,
  },
  dark: {
    "not-measuring": tokens.ink.dark.muted,
    "on-track": tokens.brand[400],
    "projected-over": tokens.money.dark.expenseOrdinary,
    "threshold-passed": tokens.money.dark.expenseOrdinary,
    exceeded: tokens.money.dark.expenseAlert,
  },
};

const BudgetMeter = ({ budget, now }: BudgetMeterProps) => {
  const progress = budgetProgress(budget, now);
  const period = budget.period;

  return (
    <Paper elevation={1} sx={{ padding: 2, display: "flex", flexDirection: "column", gap: 1 }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "baseline", gap: 1 }}>
        <Typography variant="h3">{budget.category.label}</Typography>
        {progress.state === "exceeded" && (
          <DeltaChip direction="up" tone="overBudget">
            Over budget
          </DeltaChip>
        )}
        {progress.state === "projected-over" && (
          <DeltaChip direction="up" tone="overBudget">
            Projected over
          </DeltaChip>
        )}
      </Box>

      {period === null ? (
        <Typography variant="body2" color="text.secondary">
          Not measuring right now.
        </Typography>
      ) : (
        <>
          <LinearProgress
            variant="determinate"
            value={Math.min(progress.spentRatio * 100, 100)}
            aria-valuenow={Math.round(progress.spentRatio * 100)}
            sx={{
              height: 8,
              borderRadius: `${tokens.radius.pill}px`,
              "& .MuiLinearProgress-bar": {
                backgroundColor: barColour.light[progress.state],
                borderRadius: `${tokens.radius.pill}px`,
                "[data-theme='dark'] &": {
                  backgroundColor: barColour.dark[progress.state],
                },
              },
            }}
          />
          <Typography variant="body2" color="text.secondary">
            {formatMoney(period.spent)} spent of {formatMoney(budget.amount)} ·{" "}
            {formatMoney(period.remaining)} left · {progress.daysRemaining} days left
          </Typography>
          {progress.hasUncounted && (
            <Typography
              variant="body2"
              sx={{
                color: tokens.money.light.expenseOrdinary,
                "[data-theme='dark'] &": { color: tokens.money.dark.expenseOrdinary },
              }}
            >
              Uncounted in another currency:{" "}
              {period.uncounted.map((amount) => formatMoney(amount)).join(" · ")}
            </Typography>
          )}
        </>
      )}
    </Paper>
  );
};

export default BudgetMeter;
