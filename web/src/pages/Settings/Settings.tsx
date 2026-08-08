import { Body1, makeStyles, tokens } from "@fluentui/react-components";
import PageHeader from "../../shell/PageHeader";

const useStyles = makeStyles({
  supporting: {
    color: tokens.colorNeutralForeground2,
  },
});

const Settings = () => {
  const styles = useStyles();

  return (
    <>
      <PageHeader title="Settings" />
      <Body1 className={styles.supporting}>
        Account, category, merchant and tag management is coming here soon.
      </Body1>
    </>
  );
};

export default Settings;
