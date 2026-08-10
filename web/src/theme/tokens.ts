export const tokens = {
  surface: {
    light: { page: "#fafafa", card: "#ffffff" },
    dark: { page: "#1f1f1f", card: "#292929" },
  },
  money: {
    light: { income: "#1c5cab", expenseOrdinary: "#9a5252", expenseAlert: "#d03b3b" },
    dark: { income: "#7fb4f0", expenseOrdinary: "#cf8f8f", expenseAlert: "#e66767" },
  },
  series: {
    light: { income: "#2a78d6", expense: "#d03b3b" },
    dark: { income: "#3987e5", expense: "#e66767" },
  },
  category: {
    light: ["#1565C0", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#4a3aa7", "#e34948"],
    dark: ["#3987e5", "#d95926", "#199e70", "#c98500", "#d55181", "#008300", "#9085e9", "#e66767"],
  },
  priority: {
    light: ["#104281", "#1c5cab", "#2a78d6", "#5598e7", "#86b6ef"],
    dark: ["#9ec5f4", "#6da7ec", "#3987e5", "#256abf", "#184f95"],
  },
} as const;
