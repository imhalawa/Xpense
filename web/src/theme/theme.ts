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
    heroNumber: { fontSize: "2rem", fontWeight: 700, letterSpacing: "-0.02em", lineHeight: 1.1 },
    h1: { fontSize: "1.5rem", fontWeight: 700, letterSpacing: "-0.015em" },
    h2: { fontSize: "1.25rem", fontWeight: 600 },
    h3: { fontSize: "1rem", fontWeight: 700 },
    body1: { fontSize: "0.9375rem", fontWeight: 400, lineHeight: 1.55 },
    body2: { fontSize: "0.8125rem", fontWeight: 400 },
    overline: {
      fontSize: "0.6875rem",
      fontWeight: 700,
      letterSpacing: "0.09em",
      textTransform: "uppercase",
    },
    numeric: { fontSize: "0.9375rem", fontWeight: 600, fontVariantNumeric: "tabular-nums" },
  },
  breakpoints: { values: { xs: 0, sm: 600, md: 900, lg: 1200, xl: 2000 } },
});

export default theme;
