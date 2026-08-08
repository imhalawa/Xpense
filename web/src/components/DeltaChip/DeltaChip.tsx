import { ReactNode } from "react";
import { Badge } from "@fluentui/react-components";
import { ArrowSortDownRegular, ArrowSortUpRegular } from "@fluentui/react-icons";

interface DeltaChipProps {
  direction: "up" | "down";
  tone: "overBudget" | "comparison";
  children: ReactNode;
}

const DeltaChip = ({ direction, tone, children }: DeltaChipProps) => {
  const arrowLabel = direction === "up" ? "increase" : "decrease";
  const icon =
    direction === "up" ? (
      <span aria-label={arrowLabel}>
        <ArrowSortUpRegular />
      </span>
    ) : (
      <span aria-label={arrowLabel}>
        <ArrowSortDownRegular />
      </span>
    );

  return (
    <Badge
      appearance="tint"
      color={tone === "overBudget" ? "danger" : "brand"}
      icon={icon}
      iconPosition="before"
      size="small">
      {children}
    </Badge>
  );
};

export default DeltaChip;
