import { ReactNode } from "react";
import { Caption1, Card, Title1 } from "@fluentui/react-components";

interface StatTileProps {
  label: string;
  value: string;
  hint?: ReactNode;
  badge?: ReactNode;
}

const StatTile = ({ label, value, hint, badge }: StatTileProps) => {
  return (
    <Card style={{ display: "flex", flexDirection: "column", gap: 6, padding: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8 }}>
        <Caption1>{label}</Caption1>
        {badge}
      </div>
      <Title1>{value}</Title1>
      {hint && <Caption1>{hint}</Caption1>}
    </Card>
  );
};

export default StatTile;
