import { useColorScheme } from "@mui/material/styles";
import ToggleButton from "@mui/material/ToggleButton";
import ToggleButtonGroup from "@mui/material/ToggleButtonGroup";
import { DarkModeIcon, LightModeIcon, SystemModeIcon } from "../../icons/icons";

const modes = [
  { value: "light", label: "Light", Icon: LightModeIcon },
  { value: "dark", label: "Dark", Icon: DarkModeIcon },
  { value: "system", label: "System", Icon: SystemModeIcon },
] as const;

const ThemeModeToggle = () => {
  const { mode, setMode } = useColorScheme();

  return (
    <ToggleButtonGroup
      size="small"
      exclusive
      value={mode ?? "system"}
      onChange={(_event, next) => next && setMode(next)}
    >
      {modes.map(({ value, label, Icon }) => (
        <ToggleButton
          key={value}
          value={value}
          aria-label={label}
          sx={{ paddingInline: 1, paddingBlock: 0.5, border: "none" }}
        >
          <Icon size={16} />
        </ToggleButton>
      ))}
    </ToggleButtonGroup>
  );
};

export default ThemeModeToggle;
