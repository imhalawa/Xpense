import { ToggleButton, makeStyles, tokens } from "@fluentui/react-components";
import { DesktopRegular, WeatherMoonRegular, WeatherSunnyRegular } from "@fluentui/react-icons";
import { ColorSchemeMode, useColorScheme } from "../../fluent/useColorScheme";

const modes = [
  { value: "light", label: "Light", icon: <WeatherSunnyRegular /> },
  { value: "dark", label: "Dark", icon: <WeatherMoonRegular /> },
  { value: "system", label: "System", icon: <DesktopRegular /> },
] as const;

const useStyles = makeStyles({
  root: {
    display: "flex",
    gap: tokens.spacingHorizontalXS,
  },
  button: {
    flexGrow: 1,
  },
});

const ThemeModeToggle = () => {
  const styles = useStyles();
  const { mode, setMode } = useColorScheme();

  return (
    <div className={styles.root} role="group" aria-label="Color scheme">
      {modes.map(({ value, label, icon }) => (
        <ToggleButton
          className={styles.button}
          key={value}
          icon={icon}
          checked={mode === value}
          aria-label={label}
          onClick={() => setMode(value as ColorSchemeMode)}
        />
      ))}
    </div>
  );
};

export default ThemeModeToggle;
