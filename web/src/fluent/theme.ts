import { createDarkTheme, createLightTheme, type Theme } from "@fluentui/react-components";
import { brandRamp } from "./brand";
import { tokens as xpenseTokens } from "../theme/tokens";

export const radius = {
  control: "12px",
  card: "16px",
  pill: "9999px",
} as const;

export const duration = {
  fast: "120ms",
  base: "200ms",
  slow: "320ms",
} as const;

export const easing = {
  entering: "cubic-bezier(.2, 0, 0, 1)",
  leaving: "cubic-bezier(.4, 0, 1, 1)",
} as const;

export const elevation = {
  light: {
    card: "0 2px 10px rgba(26, 34, 48, .07)",
    raised: "0 8px 20px rgba(26, 34, 48, .10)",
    overlay: "0 16px 32px rgba(26, 34, 48, .16)",
  },
  dark: {
    card: "none",
    raised: "none",
    overlay: "0 16px 32px rgba(0, 0, 0, .48)",
  },
} as const;

const fontFamily =
  "'Manrope Variable', 'Manrope', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif";

const shared = {
  fontFamilyBase: fontFamily,
  fontSizeBase300: "15px",
  lineHeightBase300: "23px",
  fontSizeBase200: "13px",
  borderRadiusSmall: "8px",
  borderRadiusMedium: radius.control,
  borderRadiusLarge: radius.card,
  borderRadiusXLarge: radius.card,
  durationFaster: duration.fast,
  durationFast: duration.fast,
  durationNormal: duration.base,
  durationSlow: duration.slow,
  durationSlower: duration.slow,
  curveEasyEase: easing.entering,
  curveDecelerateMid: easing.entering,
  curveAccelerateMid: easing.leaving,
} satisfies Partial<Theme>;

export const lightTheme: Theme = {
  ...createLightTheme(brandRamp),
  ...shared,
  colorNeutralBackground2: xpenseTokens.surface.light.page,
  colorNeutralBackground1: xpenseTokens.surface.light.card,
  shadow4: elevation.light.card,
  shadow8: elevation.light.raised,
  shadow16: elevation.light.overlay,
};

export const darkTheme: Theme = {
  ...createDarkTheme(brandRamp),
  ...shared,
  colorNeutralBackground2: xpenseTokens.surface.dark.page,
  colorNeutralBackground1: xpenseTokens.surface.dark.card,
  shadow16: elevation.dark.overlay,
};
