import { ReactNode } from "react";
import Box from "@mui/material/Box";
import { ArrowDownIcon, ArrowUpIcon } from "../../icons/icons";
import { tokens } from "../../theme/tokens";

interface DeltaChipProps {
  direction: "up" | "down";
  tone: "overBudget" | "comparison";
  children: ReactNode;
}

const DeltaChip = ({ direction, tone, children }: DeltaChipProps) => {
  const Arrow = direction === "up" ? ArrowUpIcon : ArrowDownIcon;
  const arrowLabel = direction === "up" ? "increase" : "decrease";

  return (
    <Box
      component="span"
      sx={{
        display: "inline-flex",
        alignItems: "center",
        gap: 0.5,
        borderRadius: `${tokens.radius.pill}px`,
        paddingInline: 1.25,
        paddingBlock: 0.375,
        fontSize: "0.75rem",
        fontWeight: 700,
        color: tokens.chip.light[tone].text,
        backgroundColor: tokens.chip.light[tone].wash,
        "[data-theme='dark'] &": {
          color: tokens.chip.dark[tone].text,
          backgroundColor: tokens.chip.dark[tone].wash,
        },
      }}
    >
      <Arrow size={13} title={arrowLabel} />
      {children}
    </Box>
  );
};

export default DeltaChip;
