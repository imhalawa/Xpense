import { PieChart } from "@mui/x-charts/PieChart";
import React from "react";
import { Grid, styled } from "@mui/material";
import { PieValueType, useDrawingArea } from "@mui/x-charts";
import { Box } from "lucide-react";

interface XpensePieProps {
  data: PieValueType[] | null;
  value: string | null;
  hideLegend?: boolean;
  height?: number;
  width?: number;
  innerRadius?: number;
  outerRadius?: number;
}

const StyledText = styled("text")(({ theme }) => ({
  fill: theme.palette.text.primary,
  textAnchor: "middle",
  dominantBaseline: "central",
  fontSize: 20,
}));

function PieCenterLabel({ children }: { children: React.ReactNode }) {
  const { width, height, left, top } = useDrawingArea();
  return (
    <StyledText x={left + width / 2} y={top + height / 2}>
      {children}
    </StyledText>
  );
}

const XpensePieChart = ({ data, value, height, width, innerRadius, outerRadius, hideLegend }: XpensePieProps) => {
  return (
    <PieChart
      width={width ?? 500}
      height={height ?? 500}
      series={[
        {
          data: data ?? [],
          innerRadius: innerRadius ?? 100,
          outerRadius: outerRadius ?? 120,
          paddingAngle: 2,
          cornerRadius: 5,
          startAngle: 0,
          endAngle: 360,
        },
      ]}
      slotProps={{
        legend: {
          hidden: hideLegend ?? true,
        },
      }}
    >
      <PieCenterLabel>{value}</PieCenterLabel>
    </PieChart>
  );
};

export default XpensePieChart;
