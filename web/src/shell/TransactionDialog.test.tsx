import { describe, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router";
import { LoadingContextProvider } from "../contexts/LoadingContext";
import { Currency } from "../typings/enums/Currency";
import { fixtureProjection } from "../vault/fixtureProjection";
import type { FixtureSeed } from "../vault/fixtureProjection";
import type {
  AccountView,
  TaxonomyValue,
  TransactionDraft,
  TransactionView,
} from "../vault/VaultProjection";
import type { QuickAddCategoryPriority } from "../transactions/quickAdd/types";
import { VaultProvider } from "../vault/VaultProvider";
import AppShell from "./AppShell";

vi.mock("../clients/notifications", () => ({
  listNotifications: vi.fn().mockResolvedValue({
    notifications: [],
    page: 1,
    pageSize: 10,
    totalItems: 0,
    totalPages: 1,
    unreadItems: 0,
  }),
  getUnreadCount: vi.fn().mockResolvedValue(0),
  markNotificationRead: vi.fn().mockResolvedValue(undefined),
  markAllNotificationsRead: vi.fn().mockResolvedValue(undefined),
}));

vi.mock("../pages/Transactions/TransactionsForm/TransactionsForm", () => ({
  default: ({
    accounts,
    activeSpace,
    onCancel,
    onSubmit,
    onCreateCategory,
    taxonomy,
    transaction,
    submitLabel = "Create",
  }: {
    accounts: AccountView[];
    activeSpace: string;
    onCancel: () => void;
    onSubmit: (draft: TransactionDraft) => Promise<void>;
    onCreateCategory: (
      label: string,
      priority: QuickAddCategoryPriority,
    ) => Promise<{ id: string; label: string }>;
    taxonomy?: TaxonomyValue[];
    transaction?: TransactionView;
    submitLabel?: string;
  }) => (
    <div>
      <output aria-label="Editing transaction">{transaction?.id ?? "new"}</output>
      <output aria-label="Transaction taxonomy">
        {taxonomy?.map((value) => `${value.kind}:${value.label}`).join(",") ?? ""}
      </output>
      <label>
        Account
        <select aria-label="Account">
          {accounts.map((account) => (
            <option key={account.id}>{account.label}</option>
          ))}
        </select>
      </label>
      <button
        type="button"
        onClick={() =>
          void onSubmit({
            id: transaction?.id ?? null,
            space: activeSpace,
            kind: "expense",
            amountMinorUnits: 1250,
            currency: Currency.EUR,
            occurredAt: "2026-08-08T12:00:00.000Z",
            accountId: accounts[0]?.id ?? "",
            categoryId: null,
            merchantLabel: null,
            tagLabels: [],
            reason: null,
          })
        }>
        {submitLabel}
      </button>
      <button type="button" onClick={onCancel}>
        Cancel
      </button>
      <button type="button" onClick={() => void onCreateCategory("New food", "High")}>
        Create category
      </button>
    </div>
  ),
}));

const baseSeed: FixtureSeed = {
  spaces: [{ id: "personal", name: "Personal", kind: "personal", canEdit: true }],
  accounts: {
    personal: [
      { id: "editable", label: "Everyday", currency: Currency.EUR, canEdit: true },
      { id: "readonly", label: "Shared view", currency: Currency.EUR, canEdit: false },
    ],
  },
  taxonomy: { personal: [] },
  transactions: { personal: [] },
};

const setWideScreen = () => {
  window.matchMedia = vi.fn().mockImplementation((query: string) => ({
    matches: query === "(min-width: 1024px)",
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  }));
};

const LocationProbe = () => {
  const location = useLocation();
  return <output aria-label="Current location">{location.pathname}</output>;
};

const renderShell = (
  seed: FixtureSeed,
  initialPath = "/budgets",
  options: {
    projection?: ReturnType<typeof fixtureProjection>;
    autoUnlock?: boolean;
  } = {},
) => {
  const projection = options.projection ?? fixtureProjection(seed);
  render(
    <MemoryRouter initialEntries={[initialPath]}>
      <VaultProvider projection={projection} autoUnlock={options.autoUnlock}>
        <LoadingContextProvider>
          <AppShell>
            <LocationProbe />
          </AppShell>
        </LoadingContextProvider>
      </VaultProvider>
    </MemoryRouter>,
  );
  return projection;
};

describe("TransactionDialog", () => {
  it("opens from another page and lists only editable accounts", async () => {
    setWideScreen();
    renderShell(baseSeed);

    const addTransaction = screen.getByRole("button", { name: "Add transaction" });
    await waitFor(() => expect(addTransaction).toBeEnabled());
    fireEvent.click(addTransaction);

    const dialog = await screen.findByRole("dialog");
    expect(screen.getByRole("status", { name: "Current location" })).toHaveTextContent(
      "/transactions/new",
    );
    expect(within(dialog).getByRole("option", { name: "Everyday" })).toBeDefined();
    expect(within(dialog).queryByRole("option", { name: "Shared view" })).toBeNull();
  });

  it("asks for a first account instead of offering a transaction nothing can hold", async () => {
    setWideScreen();
    renderShell({ ...baseSeed, accounts: { personal: [] } }, "/transactions/new");

    await waitFor(() =>
      expect(
        screen.getByText("Start with an account, then record what moves through it."),
      ).toBeDefined(),
    );
    expect(screen.queryByRole("button", { name: "Add transaction" })).toBeNull();
    expect(screen.getAllByRole("button", { name: "Add account" }).length).toBeGreaterThan(0);
  });

  it("disables creation and explains when an account exists but none can be edited", async () => {
    setWideScreen();
    renderShell(
      {
        ...baseSeed,
        accounts: {
          personal: [
            { id: "readonly", label: "Shared view", currency: Currency.EUR, canEdit: false },
          ],
        },
      },
      "/transactions/new",
    );

    const addTransaction = screen.getByRole("button", { name: "Add transaction" });
    await waitFor(() => expect(addTransaction).toBeDisabled());
    expect(addTransaction).toHaveAccessibleDescription(
      "No account in this space can be edited.",
    );
  });

  it("closes with Escape and returns focus to Add transaction", async () => {
    setWideScreen();
    renderShell(baseSeed);

    const addTransaction = screen.getByRole("button", { name: "Add transaction" });
    await waitFor(() => expect(addTransaction).toBeEnabled());
    fireEvent.click(addTransaction);
    const dialog = await screen.findByRole("dialog");
    await waitFor(() =>
      expect(dialog).toContainElement(document.activeElement as HTMLElement | null),
    );

    fireEvent.keyDown(dialog, { key: "Escape" });

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(screen.getByRole("status", { name: "Current location" })).toHaveTextContent("/budgets");
    await waitFor(() => expect(addTransaction).toHaveFocus());
  });

  it("saves through the projection and leaves the creation route", async () => {
    setWideScreen();
    const projection = renderShell(baseSeed, "/transactions/new");
    const saveTransaction = vi.spyOn(projection, "saveTransaction");

    fireEvent.click(await screen.findByRole("button", { name: "Create" }));

    await waitFor(() => expect(saveTransaction).toHaveBeenCalledTimes(1));
    await waitFor(() =>
      expect(screen.getByRole("status", { name: "Current location" })).toHaveTextContent(
        "/transactions",
      ),
    );
  });

  it("loads all Quick Add taxonomy on the new route and wires category creation", async () => {
    setWideScreen();
    const projection = renderShell(
      {
        ...baseSeed,
        taxonomy: {
          personal: [
            { id: "7", kind: "category", label: "Food", foregroundHex: null, backgroundHex: null },
            { id: "3", kind: "merchant", label: "Bakery", foregroundHex: null, backgroundHex: null },
            { id: "5", kind: "tag", label: "Family", foregroundHex: null, backgroundHex: null },
          ],
        },
      },
      "/transactions/new",
    );
    const createCategory = vi.spyOn(projection, "createCategory");

    await waitFor(() =>
      expect(screen.getByRole("status", { name: "Transaction taxonomy" })).toHaveTextContent(
        "category:Food,merchant:Bakery,tag:Family",
      ),
    );
    fireEvent.click(screen.getByRole("button", { name: "Create category" }));

    await waitFor(() =>
      expect(createCategory).toHaveBeenCalledWith("personal", "New food", "High"),
    );
  });

  it("loads a concrete record into the route-backed edit form and saves it by id", async () => {
    setWideScreen();
    const editable = {
      id: "11",
      kind: "expense" as const,
      amountMinorUnits: 1250,
      currency: Currency.EUR,
      occurredAt: "2026-08-08T12:00:00.000Z",
      accountId: "editable",
      counterpartyAccountId: null,
      isCounterpartyPrivate: false,
      categoryId: null,
      merchantId: null,
      tagIds: [],
      canEdit: true,
    };
    const projection = renderShell(
      { ...baseSeed, transactions: { personal: [editable] } },
      "/transactions/11/edit?space=personal",
    );
    const saveTransaction = vi.spyOn(projection, "saveTransaction");

    expect(await screen.findByRole("heading", { name: "Edit transaction" })).toBeDefined();
    expect(await screen.findByRole("status", { name: "Editing transaction" })).toHaveTextContent("11");
    expect(screen.queryByRole("button", { name: "Create" })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(saveTransaction).toHaveBeenCalledWith(expect.objectContaining({ id: "11" })));
    expect(screen.getByRole("status", { name: "Current location" })).toHaveTextContent("/transactions");
  });

  it("does not mount an edit form until its concrete transaction has loaded", async () => {
    setWideScreen();
    const editable: TransactionView = {
      id: "11",
      kind: "expense",
      amountMinorUnits: 1250,
      currency: Currency.EUR,
      occurredAt: "2026-08-08T12:00:00.000Z",
      accountId: "editable",
      counterpartyAccountId: null,
      isCounterpartyPrivate: false,
      categoryId: null,
      merchantId: null,
      tagIds: [],
      canEdit: true,
    };
    const projection = fixtureProjection({
      ...baseSeed,
      transactions: { personal: [editable] },
    });
    let finishLoading: ((transaction: TransactionView) => void) | undefined;
    vi.spyOn(projection, "getTransaction").mockReturnValue(
      new Promise((resolve) => {
        finishLoading = resolve;
      }),
    );
    const saveTransaction = vi.spyOn(projection, "saveTransaction");
    renderShell(baseSeed, "/transactions/11/edit", { projection });

    expect(await screen.findByText("Loading transaction…")).toBeVisible();
    expect(screen.queryByRole("status", { name: "Editing transaction" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Create" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Save" })).toBeNull();

    await act(async () => finishLoading?.(editable));
    expect(await screen.findByRole("status", { name: "Editing transaction" })).toHaveTextContent("11");
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() =>
      expect(saveTransaction).toHaveBeenCalledWith(expect.objectContaining({ id: "11" })),
    );
  });

  it.each([
    ["new", "/transactions/new"],
    ["edit", "/transactions/11/edit"],
  ])("keeps the locked %s route non-modal and loads it after global unlock", async (_name, route) => {
    setWideScreen();
    const editable: TransactionView = {
      id: "11",
      kind: "expense",
      amountMinorUnits: 1250,
      currency: Currency.EUR,
      occurredAt: "2026-08-08T12:00:00.000Z",
      accountId: "editable",
      counterpartyAccountId: null,
      isCounterpartyPrivate: false,
      categoryId: null,
      merchantId: null,
      tagIds: [],
      canEdit: true,
    };
    const lockedSeed = { ...baseSeed, transactions: { personal: [editable] } };
    const projection = fixtureProjection(lockedSeed);
    projection.lock();
    const getTransaction = vi.spyOn(projection, "getTransaction");
    renderShell(lockedSeed, route, { projection, autoUnlock: false });

    expect(screen.queryByRole("dialog")).toBeNull();
    fireEvent.click(await screen.findByRole("button", { name: "local" }));
    fireEvent.click(screen.getByRole("menuitem", { name: "Unlock vault" }));

    expect(await screen.findByRole("dialog")).toBeVisible();
    if (route.includes("/edit")) {
      expect(await screen.findByRole("status", { name: "Editing transaction" })).toHaveTextContent("11");
      expect(getTransaction).toHaveBeenCalledWith("personal", "11");
    } else {
      expect(await screen.findByRole("status", { name: "Editing transaction" })).toHaveTextContent("new");
    }
  });
});
