import { Body1, makeStyles, tokens } from "@fluentui/react-components";

const useStyles = makeStyles({
  supporting: {
    color: tokens.colorNeutralForeground2,
  },
});

const Settings = () => {
  const styles = useStyles();

  return (
    <Body1 className={styles.supporting}>
      Account, category, merchant and tag management is coming here soon.
    </Body1>
  );
};

export default Settings;
