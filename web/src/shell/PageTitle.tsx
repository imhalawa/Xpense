import { Subtitle1, makeStyles, tokens } from "@fluentui/react-components";
import { useIsWideScreen } from "./useIsWideScreen";

const useStyles = makeStyles({
  clipped: {
    position: "absolute",
    width: "1px",
    height: "1px",
    margin: "-1px",
    padding: 0,
    border: 0,
    clip: "rect(0px, 0px, 0px, 0px)",
    overflow: "hidden",
    whiteSpace: "nowrap",
  },
  visible: {
    display: "block",
    marginBlockEnd: tokens.spacingVerticalM,
  },
});

interface PageTitleProps {
  title: string;
}

const PageTitle = ({ title }: PageTitleProps) => {
  const styles = useStyles();
  const isWideScreen = useIsWideScreen();

  return (
    <Subtitle1 as="h1" className={isWideScreen ? styles.clipped : styles.visible}>
      {title}
    </Subtitle1>
  );
};

export default PageTitle;
