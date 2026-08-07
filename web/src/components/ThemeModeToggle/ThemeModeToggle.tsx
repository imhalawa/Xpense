import { useColorScheme } from "@mui/material/styles";
import ToggleButton from "@mui/material/ToggleButton";
import ToggleButtonGroup from "@mui/material/ToggleButtonGroup";

const modes = ["light", "dark", "system"] as const;

const labels: Record<(typeof modes)[number], string> = {
  light: "Light",
  dark: "Dark",
  system: "System",
};

const ThemeModeToggle = () => {
  const { mode, setMode } = useColorScheme();

  return (
    <ToggleButtonGroup
      size="small"
      exclusive
      value={mode ?? "system"}
      onChange={(_event, next) => next && setMode(next)}
    >
      {modes.map((option) => (
        <ToggleButton key={option} value={option} aria-label={labels[option]}>
          {labels[option]}
        </ToggleButton>
      ))}
    </ToggleButtonGroup>
  );
};

export default ThemeModeToggle;
