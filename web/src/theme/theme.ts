import { createTheme } from "@mui/material/styles";
import { tokens } from "./tokens";

declare module "@mui/material/styles" {
  interface CssThemeVariables {
    enabled: true;
  }
}

const theme = createTheme({
  cssVariables: { colorSchemeSelector: "data-theme" },
  colorSchemes: {
    light: {
      palette: {
        mode: "light",
        primary: { main: tokens.brand[500], dark: tokens.brand[600], light: tokens.brand[300] },
        background: { default: tokens.surface.light.page, paper: tokens.surface.light.card },
        text: {
          primary: tokens.ink.light.primary,
          secondary: tokens.ink.light.secondary,
          disabled: tokens.ink.light.muted,
        },
      },
    },
    dark: {
      palette: {
        mode: "dark",
        primary: { main: tokens.brand[400], dark: tokens.brand[500], light: tokens.brand[200] },
        background: { default: tokens.surface.dark.page, paper: tokens.surface.dark.card },
        text: {
          primary: tokens.ink.dark.primary,
          secondary: tokens.ink.dark.secondary,
          disabled: tokens.ink.dark.muted,
        },
      },
    },
  },
  shape: { borderRadius: tokens.radius.card },
  typography: {
    fontFamily: `"Manrope", -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif`,
    fontSize: 15,
    fontWeightLight: 300,
    fontWeightRegular: 400,
    fontWeightMedium: 600,
    fontWeightBold: 700,
  },
  breakpoints: { values: { xs: 0, sm: 600, md: 900, lg: 1200, xl: 2000 } },
});

export default theme;
