import { ReactNode } from "react";
import { Dayjs } from "dayjs";
import {
  Caption1,
  Card,
  ProgressBar,
  Subtitle2,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { IBudgetResponse } from "../../clients/types";
import type { BudgetView } from "../../vault/VaultProjection";
import { budgetProgress, BudgetState } from "../../budgets/budgetProgress";
import { formatMoney } from "../../money/formatMoney";
import DeltaChip from "../DeltaChip/DeltaChip";

interface BudgetMeterProps {
  budget: IBudgetResponse | BudgetView;
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

const useStyles = makeStyles({
  card: {
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalM,
    padding: tokens.spacingHorizontalL,
  },
  header: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "baseline",
    gap: tokens.spacingHorizontalM,
  },
  secondary: {
    color: tokens.colorNeutralForeground2,
  },
  muted: {
    color: tokens.colorNeutralForeground3,
  },
  danger: {
    color: tokens.colorStatusDangerForeground2,
  },
  actions: {
    marginTop: "auto",
    display: "flex",
    justifyContent: "flex-end",
    gap: tokens.spacingHorizontalS,
  },
});

const BudgetMeter = ({ budget, now, actions }: BudgetMeterProps) => {
  const styles = useStyles();
  const progressInput: IBudgetResponse = {
    ...budget,
    id: 0,
    category: {
      id: 0,
      label: budget.category.label,
      priority: { id: 0, label: "", weight: 0, createdAt: "", updatedAt: null },
      createdAt: "",
      updatedAt: null,
    },
  };
  const progress = budgetProgress(progressInput, now);
  const period = budget.period;
  const progressPercent = Math.min(progress.spentRatio, 1);

  return (
    <Card className={styles.card}>
      <div className={styles.header}>
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
        <Caption1 className={styles.muted}>Not measuring right now.</Caption1>
      ) : (
        <>
          <ProgressBar
            value={progressPercent}
            max={1}
            color={barToneByState[progress.state]}
            thickness="medium"
          />
          <Caption1 className={styles.secondary}>
            {formatMoney(period.spent)} spent of {formatMoney(budget.amount)} ·{" "}
            {formatMoney(period.remaining)} left · {progress.daysRemaining} days left
          </Caption1>
          {progress.hasUncounted && (
            <Caption1 className={styles.danger}>
              Uncounted in another currency:{" "}
              {period.uncounted.map((amount) => formatMoney(amount)).join(" · ")}
            </Caption1>
          )}
        </>
      )}

      {actions && <div className={styles.actions}>{actions}</div>}
    </Card>
  );
};

export default BudgetMeter;
