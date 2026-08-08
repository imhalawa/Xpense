import { ReactNode } from "react";
import { Caption1, Title2, makeStyles, tokens } from "@fluentui/react-components";

interface PageHeaderProps {
  title: string;
  description?: string;
  actions?: ReactNode;
}

const useStyles = makeStyles({
  root: {
    display: "flex",
    alignItems: "flex-start",
    justifyContent: "space-between",
    gap: tokens.spacingHorizontalL,
    marginBottom: tokens.spacingVerticalXXL,
    paddingBottom: tokens.spacingVerticalL,
    borderBottom: `${tokens.strokeWidthThin} solid ${tokens.colorBrandStroke2}`,
    "@media (max-width: 479px)": {
      flexDirection: "column",
      gap: tokens.spacingVerticalM,
    },
  },
  titleBlock: {
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalXS,
  },
  description: {
    color: tokens.colorNeutralForeground2,
  },
  actions: {
    display: "flex",
    alignItems: "center",
    gap: tokens.spacingHorizontalS,
    "@media (max-width: 479px)": {
      alignSelf: "stretch",
      justifyContent: "flex-end",
    },
  },
});

const PageHeader = ({ title, description, actions }: PageHeaderProps) => {
  const styles = useStyles();

  return (
    <header className={styles.root}>
      <div className={styles.titleBlock}>
        <Title2 as="h1">{title}</Title2>
        {description && <Caption1 className={styles.description}>{description}</Caption1>}
      </div>
      {actions && <div className={styles.actions}>{actions}</div>}
    </header>
  );
};

export default PageHeader;
