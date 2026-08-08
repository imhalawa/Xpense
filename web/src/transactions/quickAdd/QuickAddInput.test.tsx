import { useState } from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { Currency } from "../../typings/enums/Currency";
import { QuickAddInput } from "./QuickAddInput";
import { QuickAddParseResult, QuickAddParserContext, QuickAddPickerRequest, QuickAddRange } from "./types";

const context: QuickAddParserContext = {
  now: new Date("2026-08-09T12:00:00.000Z"),
  defaultAccountId: "cash",
  accounts: [{ id: "cash", label: "Cash", currency: Currency.EUR }],
  categories: [{ id: "shopping", label: "Shopping" }],
  merchants: [{ id: "albert", label: "Albert Heijn" }],
  tags: [{ id: "family", label: "family" }],
};

interface HarnessProps {
  onParseResult?: (result: QuickAddParseResult) => void;
  onOpenPicker?: (request: QuickAddPickerRequest) => void;
  onRangeFocus?: (range: QuickAddRange, result: QuickAddParseResult) => void;
}

const Harness = ({
  onParseResult = vi.fn(),
  onOpenPicker,
  onRangeFocus,
}: HarnessProps) => {
  const [value, setValue] = useState("");
  return (
    <QuickAddInput
      value={value}
      context={context}
      onValueChange={setValue}
      onParseResult={onParseResult}
      onOpenPicker={onOpenPicker}
      onRangeFocus={onRangeFocus}
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
    expect(screen.getByRole("status")).toHaveTextContent("Category #shopping recognized");
    expect(screen.getByRole("status")).not.toHaveTextContent("Spent 5 euros at Albert Heijn");
    expect(decoration.querySelector('[data-field="category"]')).toHaveAttribute(
      "data-signal",
      "solid-underline"
    );
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

  it("announces pending category creation and exposes its editable default priority", () => {
    render(<Harness />);

    fireEvent.change(screen.getByRole("textbox", { name: "Quick Add" }), {
      target: { value: "Spent 5 euros at Albert Heijn #unknown" },
    });

    expect(screen.getByRole("status")).toHaveTextContent("Category #unknown recognized");
    expect(screen.getByText("Ready to review in the transaction form")).toBeVisible();
    expect(
      screen.getByRole("button", {
        name: "Category: #unknown, recognized, priority Medium",
      })
    ).toHaveTextContent("Category · #unknown · Medium");
  });

  it("opens the matching picker and lets it rewrite only the selected range", () => {
    const onRangeFocus = vi.fn();
    const onOpenPicker = vi.fn((request: QuickAddPickerRequest) => {
      request.rewrite("#Shopping");
    });
    render(<Harness onOpenPicker={onOpenPicker} onRangeFocus={onRangeFocus} />);
    const input = screen.getByRole("textbox", { name: "Quick Add" }) as HTMLInputElement;
    fireEvent.change(input, { target: { value: "Spent 5 euros at Albert Heijn #unknown" } });

    const categoryChip = screen.getByRole("button", {
      name: "Category: #unknown, recognized, priority Medium",
    });
    expect(categoryChip).toHaveAttribute("aria-haspopup", "dialog");
    fireEvent.click(categoryChip);

    expect(onOpenPicker).toHaveBeenCalledTimes(1);
    expect(onRangeFocus).toHaveBeenCalledWith(
      expect.objectContaining({ field: "category", text: "#unknown" }),
      expect.objectContaining({ canSubmit: true })
    );
    expect(input.value).toBe("Spent 5 euros at Albert Heijn #Shopping");
  });
});
