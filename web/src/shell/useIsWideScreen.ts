import { useEffect, useState } from "react";

export const wideScreenQuery = "(min-width: 1024px)";

export const useIsWideScreen = (): boolean => {
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
