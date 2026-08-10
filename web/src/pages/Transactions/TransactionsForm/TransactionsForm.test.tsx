import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { LoadingContextProvider } from "../../../contexts/LoadingContext";
import { Currency } from "../../../typings/enums/Currency";
import type {
  AccountView,
  TaxonomyValue,
  TransactionDraft,
  TransactionView,
} from "../../../vault/VaultProjection";
import type { QuickAddCategoryPriority } from "../../../transactions/quickAdd/types";
import TransactionsForm from "./TransactionsForm";

vi.mock("../../../components/Forms/AutoComplete/AccountAutoComplete/AccountAutoComplete", () => ({
  default: ({
    error,
    helperText,
    label,
    onChange,
    options,
    value,
  }: {
    error?: boolean;
    helperText?: string;
    label: string;
    onChange: (value: { accountNumber: string; label: string } | null) => void;
    options: Array<{ accountNumber: string; label: string }>;
    value: { accountNumber: string; label: string } | null;
  }) => (
    <label>
      {label}
      <select
        aria-label={label}
        aria-invalid={error || undefined}
        value={value?.accountNumber ?? ""}
        onChange={(event) =>
          onChange(options.find((option) => option.accountNumber === event.target.value) ?? null)
        }>
        <option value="">None</option>
        {options.map((option) => (
          <option key={option.accountNumber} value={option.accountNumber}>{option.label}</option>
        ))}
      </select>
      {error && <span>{helperText}</span>}
    </label>
  ),
}));

vi.mock("../../../components/Forms/AutoComplete/CategoryAutoComplete/CategoryAutoComplete", () => ({
  default: ({ value }: { value: { label: string } | null }) => (
    <output aria-label="Category">{value?.label ?? "none"}</output>
  ),
}));

vi.mock("../../../components/Forms/AutoComplete/MerchantAutoComplete/MerchantAutoComplete", () => ({
  default: ({ value }: { value: { label: string } | null }) => (
    <output aria-label="Merchant">{value?.label ?? "none"}</output>
  ),
}));

vi.mock("../../../components/Forms/AutoComplete/TagAutoComplete/TagAutoComplete", () => ({
  default: ({ value }: { value: { label: string }[] | null }) => (
    <output aria-label="Tags">{value?.map((tag) => tag.label).join(",") ?? ""}</output>
  ),
}));

const accounts: AccountView[] = [
  { id: "cash", label: "Cash", currency: Currency.EUR, canEdit: true },
  { id: "savings", label: "Savings", currency: Currency.EUR, canEdit: true },
  { id: "dollars", label: "Dollars", currency: Currency.USD, canEdit: true },
];

const taxonomy: TaxonomyValue[] = [
  { id: "7", kind: "category", label: "Food", foregroundHex: null, backgroundHex: null },
  { id: "3", kind: "merchant", label: "Bakery", foregroundHex: null, backgroundHex: null },
  { id: "5", kind: "tag", label: "Family", foregroundHex: null, backgroundHex: null },
];

const renderForm = (
  onSubmit = vi.fn<(_: TransactionDraft) => Promise<void>>().mockResolvedValue(undefined),
  onCreateCategory?: (
    label: string,
    priority: QuickAddCategoryPriority,
  ) => Promise<{ id: string; label: string }>,
  options: {
    transaction?: TransactionView;
    submitLabel?: string;
    accounts?: AccountView[];
  } = {},
) => {
  render(
    <LoadingContextProvider>
      <TransactionsForm
        accounts={options.accounts ?? accounts}
        activeSpace="personal"
        taxonomy={taxonomy}
        onCancel={vi.fn()}
        onSubmit={onSubmit}
        onCreateCategory={onCreateCategory}
        transaction={options.transaction}
        submitLabel={options.submitLabel}
      />
    </LoadingContextProvider>,
  );
  return onSubmit;
};

describe("TransactionsForm quick add", () => {
  it("maps a parsed sentence into fields that remain reviewable before save", async () => {
    const onSubmit = renderForm();
    fireEvent.change(screen.getByRole("textbox", { name: "Quick Add" }), {
      target: { value: "spent 12 at Bakery #Food ~Family yesterday" },
    });

    await waitFor(() =>
      expect(screen.getByRole("status", { name: "Merchant" })).toHaveTextContent("Bakery"),
    );
    expect(screen.getByRole("spinbutton", { name: "Amount" })).toHaveValue(12);
    expect(screen.getByRole("status", { name: "Merchant" })).toHaveTextContent("Bakery");
    expect(screen.getByRole("status", { name: "Category" })).toHaveTextContent("Food");
    expect(screen.getByRole("status", { name: "Tags" })).toHaveTextContent("Family");

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Create" }));
    });

    await waitFor(() =>
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({
          kind: "expense",
          amountMinorUnits: 1200,
          accountId: "cash",
          categoryId: "7",
          merchantLabel: "Bakery",
          tagLabels: ["Family"],
          reason: null,
        }),
      ),
    );
  });

  it("shows a pending parsed category and delegates its creation through the supplied callback", async () => {
    const createCategory = vi.fn().mockResolvedValue({ id: "new-food", label: "New food" });
    const onSubmit = renderForm(undefined, createCategory);
    fireEvent.change(screen.getByRole("textbox", { name: "Quick Add" }), {
      target: { value: "spent 12 at Bakery #\"New food\"" },
    });

    await waitFor(() => expect(screen.getByText(/will be created before this transaction/)).toBeVisible());
    fireEvent.click(screen.getByRole("combobox", { name: "New category priority" }));
    fireEvent.click(await screen.findByRole("option", { name: "High" }));
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Create" }));
    });

    await waitFor(() => expect(createCategory).toHaveBeenCalledWith("New food", "High"));
    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ categoryId: "new-food" }));
  });

  it("applies valid partial fields but blocks stale values while the latest text has an issue", async () => {
    const onSubmit = renderForm();
    const quickAdd = screen.getByRole("textbox", { name: "Quick Add" });
    fireEvent.change(quickAdd, {
      target: { value: "spent 12 at Bakery #Food" },
    });
    await waitFor(() => expect(screen.getByRole("spinbutton", { name: "Amount" })).toHaveValue(12));

    fireEvent.change(quickAdd, {
      target: { value: "spent 18 merchant:\"Bakery\" #Food extra words" },
    });

    await waitFor(() => expect(screen.getByRole("spinbutton", { name: "Amount" })).toHaveValue(18));
    expect(screen.getAllByText("extra words was not understood").length).toBeGreaterThan(0);
    expect(screen.getByRole("button", { name: "Create" })).toBeDisabled();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("blocks a conflicting latest parse instead of saving earlier valid values", async () => {
    const onSubmit = renderForm();
    const quickAdd = screen.getByRole("textbox", { name: "Quick Add" });
    fireEvent.change(quickAdd, {
      target: { value: "spent 12 at Bakery #Food" },
    });
    await waitFor(() => expect(screen.getByRole("spinbutton", { name: "Amount" })).toHaveValue(12));

    fireEvent.change(quickAdd, {
      target: { value: "spent 18 20 at Bakery #Food" },
    });
    await waitFor(() => expect(screen.getByRole("button", { name: "Create" })).toBeDisabled());
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("opens a range chip at its form control and rewrites the source when reviewed", async () => {
    renderForm();
    const quickAdd = screen.getByRole("textbox", { name: "Quick Add" });
    fireEvent.change(quickAdd, {
      target: { value: "spent 12 at Bakery #Food" },
    });

    const amountChip = await screen.findByRole("button", {
      name: "Amount: 12, recognized",
    });
    fireEvent.click(amountChip);
    const amount = screen.getByRole("spinbutton", { name: "Amount" });
    await waitFor(() => expect(amount).toHaveFocus());
    fireEvent.change(amount, { target: { value: "19" } });

    await waitFor(() => expect(quickAdd).toHaveValue("spent 19 at Bakery #Food"));
  });

  it("clears expense-only values when Quick Add changes the draft to a transfer", async () => {
    const onSubmit = renderForm();
    const quickAdd = screen.getByRole("textbox", { name: "Quick Add" });
    fireEvent.change(quickAdd, {
      target: { value: "spent 12 at Bakery #Food" },
    });
    await waitFor(() =>
      expect(screen.getByRole("status", { name: "Category" })).toHaveTextContent("Food"),
    );
    expect(screen.queryByRole("textbox", { name: "Note" })).toBeNull();

    fireEvent.change(quickAdd, { target: { value: "moved 5 @Cash >Savings" } });

    await waitFor(() => expect(screen.getByRole("textbox", { name: "Note" })).toBeVisible());
    expect(screen.queryByRole("status", { name: "Category" })).toBeNull();
    expect(screen.queryByRole("status", { name: "Merchant" })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Create" }));

    await waitFor(() =>
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({
          kind: "transfer",
          categoryId: null,
          merchantLabel: null,
          accountId: "cash",
          counterpartyAccountId: "savings",
        }),
      ),
    );
  });

  it("shows an accessible error when saving fails", async () => {
    const onSubmit = vi.fn().mockRejectedValue(new Error("Server unavailable"));
    renderForm(onSubmit);
    fireEvent.change(screen.getByRole("textbox", { name: "Quick Add" }), {
      target: { value: "spent 12 at Bakery #Food" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Create" }));

    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent("Server unavailable"),
    );
  });

  it("blocks a pending category when no creation callback is available", async () => {
    const onSubmit = renderForm();
    fireEvent.change(screen.getByRole("textbox", { name: "Quick Add" }), {
      target: { value: "spent 12 at Bakery #\"New food\"" },
    });
    await screen.findByText(/will be created before this transaction/);
    fireEvent.click(screen.getByRole("button", { name: "Create" }));

    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(
        "Category “New food” must be created before saving.",
      ),
    );
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("reuses a category that was created before a failed transaction retry", async () => {
    const createCategory = vi.fn().mockResolvedValue({ id: "new-food", label: "New food" });
    const onSubmit = vi
      .fn()
      .mockRejectedValueOnce(new Error("Transaction save failed"))
      .mockResolvedValueOnce(undefined);
    renderForm(onSubmit, createCategory);
    fireEvent.change(screen.getByRole("textbox", { name: "Quick Add" }), {
      target: { value: "spent 12 at Bakery #\"New food\"" },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Create" }));
    await screen.findByText("Transaction save failed");
    expect(screen.getByText(/was created and will be reused/)).toBeVisible();
    fireEvent.change(screen.getByRole("textbox", { name: "Quick Add" }), {
      target: { value: "spent 13 at Bakery #\"New food\"" },
    });
    await waitFor(() => expect(screen.getByRole("spinbutton", { name: "Amount" })).toHaveValue(13));
    fireEvent.click(screen.getByRole("button", { name: "Create" }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(2));
    expect(createCategory).toHaveBeenCalledTimes(1);
    expect(onSubmit).toHaveBeenLastCalledWith(
      expect.objectContaining({ categoryId: "new-food" }),
    );
  });

  it("blocks same-account and cross-currency transfers after the source changes", async () => {
    const onSubmit = renderForm();
    fireEvent.change(screen.getByRole("textbox", { name: "Quick Add" }), {
      target: { value: "moved 5 @Cash >Savings" },
    });
    await screen.findByRole("textbox", { name: "Note" });

    await act(async () => {
      fireEvent.change(screen.getByRole("combobox", { name: "Account" }), {
        target: { value: "savings" },
      });
    });
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Create" }));
    });
    expect(await screen.findByText("Choose a different destination account")).toBeVisible();
    expect(onSubmit).not.toHaveBeenCalled();

    await act(async () => {
      fireEvent.change(screen.getByRole("combobox", { name: "Account" }), {
        target: { value: "dollars" },
      });
    });
    expect(await screen.findByText("Transfer accounts must use the same currency")).toBeVisible();
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Create" }));
    });
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("populates edits and submits the existing id with a Save action", async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    const transaction: TransactionView = {
      id: "11",
      kind: "expense",
      amountMinorUnits: 3456,
      currency: Currency.EUR,
      occurredAt: "2026-08-08T12:00:00.000Z",
      accountId: "cash",
      counterpartyAccountId: null,
      isCounterpartyPrivate: false,
      categoryId: "7",
      merchantId: "3",
      tagIds: ["5"],
      reason: null,
      canEdit: true,
    };
    renderForm(onSubmit, undefined, { transaction, submitLabel: "Save" });

    expect(screen.getByRole("spinbutton", { name: "Amount" })).toHaveValue(34.56);
    expect(screen.queryByRole("button", { name: "Create" })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() =>
      expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ id: "11" })),
    );
  });
});
