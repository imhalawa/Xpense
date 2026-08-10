import { ReactNode } from "react";
import { Caption1, Card, Title1, makeStyles, tokens } from "@fluentui/react-components";

interface StatTileProps {
  label: string;
  value: string;
  hint?: ReactNode;
  badge?: ReactNode;
}

const useStyles = makeStyles({
  card: {
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalXS,
    padding: tokens.spacingHorizontalL,
  },
  heading: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    gap: tokens.spacingHorizontalS,
  },
  supporting: {
    color: tokens.colorNeutralForeground2,
  },
});

const StatTile = ({ label, value, hint, badge }: StatTileProps) => {
  const styles = useStyles();

  return (
    <Card className={styles.card} size="large">
      <div className={styles.heading}>
        <Caption1 className={styles.supporting}>{label}</Caption1>
        {badge}
      </div>
      <Title1>{value}</Title1>
      {hint && <Caption1 className={styles.supporting}>{hint}</Caption1>}
    </Card>
  );
};

export default StatTile;
