import { ReactNode } from "react";
import { Dayjs } from "dayjs";
import { Caption1, Card, ProgressBar, Subtitle2 } from "@fluentui/react-components";
import { IBudgetResponse } from "../../clients/types";
import { budgetProgress, BudgetState } from "../../budgets/budgetProgress";
import { formatMoney } from "../../money/formatMoney";
import DeltaChip from "../DeltaChip/DeltaChip";

interface BudgetMeterProps {
  budget: IBudgetResponse;
  now: Dayjs;
  actions?: ReactNode;
}

const barToneByState: Record<BudgetState, "brand" | "warning" | "error" | "success"> = {
  "not-measuring": "brand",
  "on-track": "success",
  "projected-over": "warning",
  "threshold-passed": "warning",
  exceeded: "error",
};

const BudgetMeter = ({ budget, now, actions }: BudgetMeterProps) => {
  const progress = budgetProgress(budget, now);
  const period = budget.period;
  const progressPercent = Math.min(progress.spentRatio, 1);

  return (
    <Card style={{ display: "flex", flexDirection: "column", gap: 12, padding: 16 }}>
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "baseline",
          gap: 12,
        }}>
        <Subtitle2>{budget.category.label}</Subtitle2>
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
      </div>

      {period === null ? (
        <Caption1 style={{ color: "var(--colorNeutralForeground3)" }}>
          Not measuring right now.
        </Caption1>
      ) : (
        <>
          <ProgressBar
            value={progressPercent}
            max={1}
            color={barToneByState[progress.state]}
            thickness="medium"
          />
          <Caption1 style={{ color: "var(--colorNeutralForeground2)" }}>
            {formatMoney(period.spent)} spent of {formatMoney(budget.amount)} ·{" "}
            {formatMoney(period.remaining)} left · {progress.daysRemaining} days left
          </Caption1>
          {progress.hasUncounted && (
            <Caption1 style={{ color: "var(--colorStatusDangerForeground2)" }}>
              Uncounted in another currency:{" "}
              {period.uncounted.map((amount) => formatMoney(amount)).join(" · ")}
            </Caption1>
          )}
        </>
      )}

      {actions && (
        <div style={{ marginTop: "auto", display: "flex", justifyContent: "flex-end", gap: 8 }}>
          {actions}
        </div>
      )}
    </Card>
  );
};

export default BudgetMeter;
