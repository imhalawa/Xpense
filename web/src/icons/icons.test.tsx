import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { ComponentType } from "react";
import { IconProps } from "./Icon";
import {
  AccountIcon,
  AlertIcon,
  AmountIcon,
  ArrowDownIcon,
  ArrowUpIcon,
  BellIcon,
  BudgetsIcon,
  CategoryIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  CloseIcon,
  DarkModeIcon,
  DateIcon,
  DollarIcon,
  EuroIcon,
  ExpenseIcon,
  IncomeIcon,
  LightModeIcon,
  ManageIcon,
  MerchantIcon,
  OverviewIcon,
  PlusIcon,
  SystemModeIcon,
  TagsIcon,
  TransactionsIcon,
} from "./icons";

const allIcons: Array<[string, ComponentType<IconProps>]> = [
  ["AccountIcon", AccountIcon],
  ["AlertIcon", AlertIcon],
  ["AmountIcon", AmountIcon],
  ["ArrowDownIcon", ArrowDownIcon],
  ["ArrowUpIcon", ArrowUpIcon],
  ["BellIcon", BellIcon],
  ["BudgetsIcon", BudgetsIcon],
  ["CategoryIcon", CategoryIcon],
  ["ChevronLeftIcon", ChevronLeftIcon],
  ["ChevronRightIcon", ChevronRightIcon],
  ["CloseIcon", CloseIcon],
  ["DarkModeIcon", DarkModeIcon],
  ["DateIcon", DateIcon],
  ["DollarIcon", DollarIcon],
  ["EuroIcon", EuroIcon],
  ["ExpenseIcon", ExpenseIcon],
  ["IncomeIcon", IncomeIcon],
  ["LightModeIcon", LightModeIcon],
  ["ManageIcon", ManageIcon],
  ["MerchantIcon", MerchantIcon],
  ["OverviewIcon", OverviewIcon],
  ["PlusIcon", PlusIcon],
  ["SystemModeIcon", SystemModeIcon],
  ["TagsIcon", TagsIcon],
  ["TransactionsIcon", TransactionsIcon],
];

const renderIcon = (IconComponent: ComponentType<IconProps>, props: IconProps = {}) =>
  render(<IconComponent {...props} />).container.querySelector("svg");

describe("icon set", () => {
  it("exports every icon of the set", () => {
    expect(allIcons.length).toBe(25);
  });

  it.each(allIcons)("%s draws on the shared 24 grid", (_name, IconComponent) => {
    expect(renderIcon(IconComponent)?.getAttribute("viewBox")).toBe("0 0 24 24");
  });

  it.each(allIcons)("%s inherits its colour from the theme", (_name, IconComponent) => {
    expect(renderIcon(IconComponent)?.getAttribute("stroke")).toBe("currentColor");
  });

  it.each(allIcons)("%s is decorative without a title", (_name, IconComponent) => {
    const svg = renderIcon(IconComponent);
    expect(svg?.getAttribute("aria-hidden")).toBe("true");
    expect(svg?.getAttribute("role")).toBe(null);
  });

  it.each(allIcons)("%s is announced when given a title", (_name, IconComponent) => {
    const svg = renderIcon(IconComponent, { title: "meaningful icon" });
    expect(svg?.getAttribute("role")).toBe("img");
    expect(svg?.getAttribute("aria-hidden")).toBe(null);
    expect(svg?.getAttribute("aria-label")).toBe("meaningful icon");
    expect(svg?.querySelector("title")?.textContent).toBe("meaningful icon");
  });

  it.each(allIcons)("%s honours the size prop", (_name, IconComponent) => {
    const svg = renderIcon(IconComponent, { size: 32 });
    expect(svg?.getAttribute("width")).toBe("32");
    expect(svg?.getAttribute("height")).toBe("32");
  });

  it.each(allIcons)("%s falls back to the default size", (_name, IconComponent) => {
    const svg = renderIcon(IconComponent);
    expect(svg?.getAttribute("width")).toBe("20");
    expect(svg?.getAttribute("height")).toBe("20");
  });
});
