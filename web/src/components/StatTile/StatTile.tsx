import { ReactNode } from "react";
import Box from "@mui/material/Box";
import Paper from "@mui/material/Paper";
import Typography from "@mui/material/Typography";

interface StatTileProps {
  label: string;
  value: string;
  hint?: ReactNode;
  badge?: ReactNode;
}

const StatTile = ({ label, value, hint, badge }: StatTileProps) => (
  <Paper elevation={1} sx={{ padding: 2, display: "flex", flexDirection: "column", gap: 0.5 }}>
    <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 1 }}>
      <Typography variant="overline" color="text.secondary">
        {label}
      </Typography>
      {badge}
    </Box>
    <Typography variant="heroNumber">{value}</Typography>
    {hint && (
      <Typography variant="body2" color="text.secondary">
        {hint}
      </Typography>
    )}
  </Paper>
);

export default StatTile;
