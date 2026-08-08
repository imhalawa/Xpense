import { useState } from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { Currency } from "../../typings/enums/Currency";
import { QuickAddInput } from "./QuickAddInput";
import { QuickAddParseResult, QuickAddParserContext } from "./types";

const context: QuickAddParserContext = {
  now: new Date("2026-08-09T12:00:00.000Z"),
  defaultAccountId: "cash",
  accounts: [{ id: "cash", label: "Cash", currency: Currency.EUR }],
  categories: [{ id: "shopping", label: "Shopping" }],
  merchants: [{ id: "albert", label: "Albert Heijn" }],
  tags: [{ id: "family", label: "family" }],
};

const Harness = ({ onParseResult = vi.fn() }: { onParseResult?: (result: QuickAddParseResult) => void }) => {
  const [value, setValue] = useState("");
  return (
    <QuickAddInput
      value={value}
      context={context}
      onValueChange={setValue}
      onParseResult={onParseResult}
    />
  );
};

describe("QuickAddInput", () => {
  it("decorates recognised text while keeping one editable sentence", () => {
    render(<Harness />);
    const input = screen.getByRole("textbox", { name: "Quick Add" });

    fireEvent.change(input, { target: { value: "Spent 5 euros at Albert Heijn #shopping" } });

    const decoration = screen.getByTestId("quick-add-decoration");
    expect(decoration.querySelector('[data-field="amount"]')?.textContent).toBe("5");
    expect(decoration.querySelector('[data-field="merchant"]')?.textContent).toBe("Albert Heijn");
    expect(decoration.querySelector('[data-field="category"]')?.textContent).toBe("#shopping");
    expect(screen.getByRole("status")).toHaveTextContent("0 issues");
  });

  it("mirrors parsed ranges as touch and keyboard accessible chips", () => {
    render(<Harness />);
    const input = screen.getByRole("textbox", { name: "Quick Add" }) as HTMLInputElement;
    fireEvent.change(input, { target: { value: "Spent 5 euros at Albert Heijn #shopping" } });

    fireEvent.click(screen.getByRole("button", { name: "Merchant: Albert Heijn, recognized" }));

    expect(document.activeElement).toBe(input);
    expect(input.selectionStart).toBe(17);
    expect(input.selectionEnd).toBe(29);
  });

  it("announces unresolved fields without repeating the whole sentence", () => {
    render(<Harness />);

    fireEvent.change(screen.getByRole("textbox", { name: "Quick Add" }), {
      target: { value: "Spent 5 euros at Albert Heijn #unknown" },
    });

    expect(screen.getByRole("status")).toHaveTextContent("1 issue");
    expect(screen.getByText("No category matches unknown")).toBeVisible();
    expect(screen.getByRole("button", { name: "Category: #unknown, unresolved" })).toBeVisible();
  });
});
