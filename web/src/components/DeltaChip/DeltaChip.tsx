import { ReactNode } from "react";
import { Badge } from "@fluentui/react-badge";
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
  const color = tone === "overBudget" ? "danger" : "warning";

  return (
    <Badge
      appearance="filled"
      color={color}
      icon={icon}
      iconPosition="before"
      size="extra-small">
      {children}
    </Badge>
  );
};

export default DeltaChip;
