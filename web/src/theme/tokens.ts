export const tokens = {
  brand: {
    50: "#E8F1FB",
    100: "#cde2fb",
    200: "#9ec5f4",
    300: "#6da7ec",
    400: "#3987e5",
    500: "#1565C0",
    600: "#184f95",
    700: "#0d366b",
  },
  surface: {
    light: { page: "#f5f7fa", card: "#FFFFFF", raised: "#FFFFFF", overlay: "#FFFFFF" },
    dark: { page: "#0F1115", card: "#191C21", raised: "#20242B", overlay: "#272C34" },
  },
  ink: {
    light: { primary: "#1a2230", secondary: "#5b6879", muted: "#748091" },
    dark: { primary: "#e8eaee", secondary: "#a7b0bd", muted: "#8b95a3" },
  },
  money: {
    light: { income: "#1c5cab", expenseOrdinary: "#9a5252", expenseAlert: "#d03b3b" },
    dark: { income: "#7fb4f0", expenseOrdinary: "#cf8f8f", expenseAlert: "#e66767" },
  },
  series: {
    light: { income: "#2a78d6", expense: "#d03b3b" },
    dark: { income: "#3987e5", expense: "#e66767" },
  },
  chip: {
    light: {
      overBudget: { text: "#a62020", wash: "#fdeaea" },
      comparison: { text: "#124e96", wash: "#e8f1fb" },
    },
    dark: {
      overBudget: { text: "#f2a3a3", wash: "#3a2323" },
      comparison: { text: "#a8cbf5", wash: "#1e2c3e" },
    },
  },
  category: {
    light: ["#1565C0", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#4a3aa7", "#e34948"],
    dark: ["#3987e5", "#d95926", "#199e70", "#c98500", "#d55181", "#008300", "#9085e9", "#e66767"],
  },
  priority: {
    light: ["#104281", "#1c5cab", "#2a78d6", "#5598e7", "#86b6ef"],
    dark: ["#9ec5f4", "#6da7ec", "#3987e5", "#256abf", "#184f95"],
  },
  radius: { card: 16, input: 12, iconTile: 12, pill: 999, chartBarEnd: 4 },
  motion: {
    fast: 120,
    base: 200,
    slow: 320,
    easingEnter: "cubic-bezier(0.2, 0, 0, 1)",
    easingExit: "cubic-bezier(0.4, 0, 1, 1)",
  },
} as const;
