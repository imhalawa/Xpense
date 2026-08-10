import { makeStaticStyles, tokens as fluentTokens } from "@fluentui/react-components";
import { tokens as xpenseTokens } from "../theme/tokens";

export const appGlobalStyles = [
  {
    ":where(a, button, [tabindex]):focus-visible": {
      outline: `${fluentTokens.strokeWidthThick} solid ${fluentTokens.colorBrandStroke1}`,
      outlineOffset: fluentTokens.strokeWidthThick,
      borderRadius: fluentTokens.borderRadiusMedium,
    },
    ":where(.fui-Input, .fui-Combobox, .fui-Select, .fui-Textarea, .fui-Dropdown, .fui-SpinButton):has(:focus-visible)": {
      outline: `${fluentTokens.strokeWidthThick} solid ${fluentTokens.colorBrandStroke1}`,
      outlineOffset: fluentTokens.strokeWidthThick,
    },
    ":where(.fui-Input, .fui-Combobox, .fui-Select, .fui-Textarea, .fui-Dropdown, .fui-SpinButton) :where(input, select, textarea):focus-visible": {
      outline: "none",
    },
    ":where(.fui-Input, .fui-Combobox, .fui-Select__select, .fui-Textarea, .fui-Dropdown, .fui-SpinButton)::after": {
      content: "none",
    },
    ":where(.fui-Input, .fui-Combobox, .fui-Select, .fui-Textarea, .fui-Dropdown, .fui-SpinButton)": {
      transitionProperty: "outline-color, border-color, background-color",
      transitionDuration: fluentTokens.durationFast,
      transitionTimingFunction: fluentTokens.curveEasyEase,
    },
    ":where(.fui-Button, .fui-ToggleButton, .fui-MenuItem, .fui-NavItem, .fui-Tab)": {
      transitionProperty: "background-color, color, border-color, box-shadow",
      transitionDuration: fluentTokens.durationFast,
      transitionTimingFunction: fluentTokens.curveEasyEase,
    },
    '[data-portal-node="true"]': {
      backgroundColor: "transparent",
    },
    ":root": {
      "--xpense-income": xpenseTokens.money.light.income,
      "--xpense-expense": xpenseTokens.money.light.expenseOrdinary,
      "--xpense-alert": xpenseTokens.money.light.expenseAlert,
      "--xpense-category-0": xpenseTokens.category.light[0],
      "--xpense-category-1": xpenseTokens.category.light[1],
      "--xpense-category-2": xpenseTokens.category.light[2],
      "--xpense-category-3": xpenseTokens.category.light[3],
      "--xpense-category-4": xpenseTokens.category.light[4],
      "--xpense-category-5": xpenseTokens.category.light[5],
      "--xpense-category-6": xpenseTokens.category.light[6],
      "--xpense-category-7": xpenseTokens.category.light[7],
    },
    '[data-theme="dark"]': {
      "--xpense-income": xpenseTokens.money.dark.income,
      "--xpense-expense": xpenseTokens.money.dark.expenseOrdinary,
      "--xpense-alert": xpenseTokens.money.dark.expenseAlert,
      "--xpense-category-0": xpenseTokens.category.dark[0],
      "--xpense-category-1": xpenseTokens.category.dark[1],
      "--xpense-category-2": xpenseTokens.category.dark[2],
      "--xpense-category-3": xpenseTokens.category.dark[3],
      "--xpense-category-4": xpenseTokens.category.dark[4],
      "--xpense-category-5": xpenseTokens.category.dark[5],
      "--xpense-category-6": xpenseTokens.category.dark[6],
      "--xpense-category-7": xpenseTokens.category.dark[7],
    },
  },
  `@media (prefers-reduced-motion: reduce) {
    *, *::before, *::after {
      animation-duration: 0.01ms !important;
      animation-iteration-count: 1 !important;
      transition-duration: 0.01ms !important;
      scroll-behavior: auto !important;
    }
  }`,
];

const useGlobalStyles = makeStaticStyles(appGlobalStyles);

const GlobalStyles = () => {
  useGlobalStyles();
  return null;
};

export default GlobalStyles;
