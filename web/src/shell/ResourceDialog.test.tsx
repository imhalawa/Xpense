import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { Currency } from "../typings/enums/Currency";
import ResourceDialog from "./ResourceDialog";
import type { ResourceDialogRequest } from "./ResourceDialog";

const trigger = document.createElement("button");
document.body.append(trigger);

const request: ResourceDialogRequest = { action: "delete", resource: { kind: "account", value: { id: "account-1", label: "Everyday", currency: Currency.EUR, canEdit: true } }, trigger };

describe("ResourceDialog", () => {
  it("confirms deletion, explains filter cleanup, and restores focus", async () => {
    const onClose = vi.fn();
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(<ResourceDialog request={request} onClose={onClose} onSubmit={onSubmit} />);

    expect(screen.getByText(/active account filter will be cleared/i)).toBeDefined();
    fireEvent.click(screen.getByRole("button", { name: "Delete" }));
    await waitFor(() => expect(onSubmit).toHaveBeenCalledWith(request, undefined));
    await waitFor(() => expect(document.activeElement).toBe(trigger));
  });

  it("offers all API priorities and preserves Essential when editing only the label", async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    const categoryRequest: ResourceDialogRequest = {
      action: "edit",
      resource: {
        kind: "category",
        value: {
          id: "category-1",
          kind: "category",
          label: "Food",
          foregroundHex: null,
          backgroundHex: null,
          priority: "Essential",
        },
      },
      trigger,
    };
    render(<ResourceDialog request={categoryRequest} onClose={vi.fn()} onSubmit={onSubmit} />);

    expect(screen.getAllByRole("option").map((option) => option.textContent)).toEqual([
      "Essential",
      "Important",
      "Useful",
      "Optional",
      "Avoidable",
    ]);
    fireEvent.change(screen.getByRole("textbox", { name: "Label" }), {
      target: { value: "Food and drink" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalledWith(
      categoryRequest,
      expect.objectContaining({ label: "Food and drink", priority: "Essential" }),
    ));
  });
});
