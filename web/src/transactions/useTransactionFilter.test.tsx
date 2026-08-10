import { describe, expect, it } from "vitest";
import { act, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, useLocation, useNavigate } from "react-router";
import { Currency } from "../typings/enums/Currency";
import { fixtureProjection } from "../vault/fixtureProjection";
import type { FixtureSeed } from "../vault/fixtureProjection";
import type { VaultProjection } from "../vault/VaultProjection";
import { useTransactionFilter } from "./useTransactionFilter";

const personalSpace = "personal";

const buildSeed = (): FixtureSeed => ({
  spaces: [{ id: personalSpace, name: "Personal", kind: "personal", canEdit: true }],
  accounts: {
    [personalSpace]: [
      { id: "account-1", label: "Everyday", currency: Currency.EUR, canEdit: true },
    ],
  },
  taxonomy: {
    [personalSpace]: [
      { id: "3", kind: "category", label: "Groceries", foregroundHex: null, backgroundHex: null },
      { id: "4", kind: "category", label: "Travel", foregroundHex: null, backgroundHex: null },
      { id: "5", kind: "merchant", label: "Albert", foregroundHex: null, backgroundHex: null },
      { id: "6", kind: "tag", label: "Work", foregroundHex: null, backgroundHex: null },
    ],
  },
  transactions: { [personalSpace]: [] },
});

type Controller = ReturnType<typeof useTransactionFilter>;

interface ProbeHandle {
  controller: Controller;
  goBack: () => void;
  messages: (string | null)[];
}

const buildHandle = (): ProbeHandle => ({
  controller: undefined as unknown as Controller,
  goBack: () => {},
  messages: [],
});

function FilterProbe({
  projection,
  handle,
}: {
  projection: VaultProjection;
  handle: ProbeHandle;
}) {
  const location = useLocation();
  const navigate = useNavigate();
  const controller = useTransactionFilter(projection, personalSpace);

  handle.controller = controller;
  handle.goBack = () => navigate(-1);
  handle.messages.push(controller.removedMessage);

  return (
    <div>
      <span data-testid="pathname">{location.pathname}</span>
      <span data-testid="search">{location.search}</span>
      <span data-testid="message">{controller.removedMessage ?? ""}</span>
      <span data-testid="active-filter-count">{controller.activeFilterCount}</span>
    </div>
  );
}

const renderFilter = (initialEntries: string[], initialIndex?: number) => {
  const handle = buildHandle();
  const projection = fixtureProjection(buildSeed());

  render(
    <MemoryRouter initialEntries={initialEntries} initialIndex={initialIndex}>
      <FilterProbe projection={projection} handle={handle} />
    </MemoryRouter>,
  );

  return handle;
};

const currentSearch = () => new URLSearchParams(screen.getByTestId("search").textContent ?? "");

const announcementCount = (messages: (string | null)[]) =>
  messages.filter(
    (message, index) => message !== null && (index === 0 || messages[index - 1] === null),
  ).length;

describe("useTransactionFilter", () => {
  it("strips an inaccessible identifier from the URL and reports it", async () => {
    const handle = renderFilter(["/transactions?category=999"]);

    await waitFor(() => expect(currentSearch().get("category")).toBeNull());

    expect(currentSearch().get("space")).toBe(personalSpace);
    expect(handle.controller.filter.category).toBeNull();
    expect(screen.getByTestId("message").textContent).toContain("category");
  });

  it("replaces the history entry rather than pushing a new one", async () => {
    const handle = renderFilter(["/transactions", "/transactions?category=999"], 1);

    await waitFor(() => expect(currentSearch().get("category")).toBeNull());

    act(() => handle.goBack());

    expect(screen.getByTestId("pathname").textContent).toBe("/transactions");
    expect(screen.getByTestId("search").textContent).toBe("");
  });

  it("reports the removal once and not again on later renders", async () => {
    const handle = renderFilter(["/transactions?category=999"]);

    await waitFor(() => expect(screen.getByTestId("message").textContent).not.toBe(""));
    await waitFor(() => expect(currentSearch().get("category")).toBeNull());

    expect(announcementCount(handle.messages)).toBe(1);

    act(() => handle.controller.dismissRemovedMessage());

    expect(screen.getByTestId("message").textContent).toBe("");

    act(() => handle.controller.setFilter({ ...handle.controller.filter, category: "999" }));

    await waitFor(() => expect(currentSearch().get("category")).toBeNull());
    expect(screen.getByTestId("message").textContent).toBe("");
    expect(announcementCount(handle.messages)).toBe(1);
  });

  it("reports again when a different identifier is inaccessible", async () => {
    const handle = renderFilter(["/transactions?category=999"]);

    await waitFor(() => expect(currentSearch().get("category")).toBeNull());
    act(() => handle.controller.dismissRemovedMessage());

    act(() => handle.controller.setFilter({ ...handle.controller.filter, merchant: "777" }));

    await waitFor(() => expect(screen.getByTestId("message").textContent).toContain("merchant"));
    expect(currentSearch().get("merchant")).toBeNull();
    expect(announcementCount(handle.messages)).toBe(2);
  });

  it("keeps the date range and account when a taxonomy filter is toggled on", async () => {
    const handle = renderFilter([
      "/transactions?from=2026-01-01&to=2026-01-31&account=account-1",
    ]);

    await waitFor(() => expect(handle.controller.filter.account).toBe("account-1"));

    act(() => handle.controller.toggleTaxonomy("category", "3"));

    await waitFor(() => expect(currentSearch().get("category")).toBe("3"));

    const search = currentSearch();
    expect(search.get("from")).toBe("2026-01-01");
    expect(search.get("to")).toBe("2026-01-31");
    expect(search.get("account")).toBe("account-1");
    expect(search.get("space")).toBe(personalSpace);
    expect(screen.getByTestId("active-filter-count").textContent).toBe("3");
  });

  it("removes only that facet when the selected value is toggled again", async () => {
    const handle = renderFilter(["/transactions?category=3&merchant=5&account=account-1"]);

    await waitFor(() => expect(handle.controller.filter.category).toBe("3"));

    act(() => handle.controller.toggleTaxonomy("category", "3"));

    await waitFor(() => expect(currentSearch().get("category")).toBeNull());

    const search = currentSearch();
    expect(search.get("merchant")).toBe("5");
    expect(search.get("account")).toBe("account-1");
  });

  it("clears every facet except the space", async () => {
    const handle = renderFilter([
      "/transactions?category=3&tag=6&from=2026-01-01&to=2026-01-31",
    ]);

    await waitFor(() => expect(handle.controller.filter.tag).toBe("6"));

    act(() => handle.controller.clearFilters());

    await waitFor(() => expect(screen.getByTestId("search").textContent).toBe("?space=personal"));
    expect(screen.getByTestId("active-filter-count").textContent).toBe("0");
  });
});
