import { useEffect, useState } from "react";
import { Subtitle1, makeStyles, tokens } from "@fluentui/react-components";

const wideScreenQuery = "(min-width: 1024px)";

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

const useIsWideScreen = () => {
  const evaluate = () => window.matchMedia(wideScreenQuery).matches;
  const [isWideScreen, setIsWideScreen] = useState<boolean>(evaluate);

  useEffect(() => {
    const mediaQuery = window.matchMedia(wideScreenQuery);
    const handleChange = () => setIsWideScreen(mediaQuery.matches);

    handleChange();
    mediaQuery.addEventListener("change", handleChange);
    return () => mediaQuery.removeEventListener("change", handleChange);
  }, []);

  return isWideScreen;
};

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
