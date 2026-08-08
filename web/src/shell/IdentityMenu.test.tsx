import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { SpaceSummary } from "../vault/VaultProjection";
import IdentityMenu from "./IdentityMenu";

const spaces: SpaceSummary[] = [
  { id: "personal", name: "Personal", kind: "personal", canEdit: true },
  { id: "household", name: "Household", kind: "group", canEdit: true },
  { id: "holiday-fund", name: "Holiday fund", kind: "group", canEdit: false },
];

const renderIdentityMenu = (overrides: Partial<Parameters<typeof IdentityMenu>[0]> = {}) => {
  const handlers = {
    onSelectSpace: vi.fn(),
    onManageGroups: vi.fn(),
    onAccountSettings: vi.fn(),
    onLock: vi.fn(),
    onSignOut: vi.fn(),
  };

  render(
    <IdentityMenu
      spaces={spaces}
      activeSpace="personal"
      displayName="Mohamed Halawa"
      emailPrefix="mohamed"
      isUnlocked
      {...handlers}
      {...overrides}
    />
  );

  return handlers;
};

const openMenu = () => {
  fireEvent.click(screen.getByRole("button", { name: /Mohamed Halawa/ }));
};

describe("IdentityMenu", () => {
  it("names the trigger with the display name and the active space", () => {
    renderIdentityMenu();

    expect(screen.getByRole("button", { name: "Mohamed Halawa, Personal" })).toBeDefined();
  });

  it("lists every space with exactly one checked", () => {
    renderIdentityMenu({ activeSpace: "household" });
    openMenu();

    const spaceItems = screen.getAllByRole("menuitemradio");

    expect(spaceItems.map((item) => item.textContent)).toEqual([
      "Personal",
      "Household",
      "Holiday fund",
    ]);
    expect(spaceItems.filter((item) => item.getAttribute("aria-checked") === "true")).toHaveLength(
      1
    );
    expect(screen.getByRole("menuitemradio", { name: "Household" }).getAttribute("aria-checked")).toBe(
      "true"
    );
  });

  it("reports the chosen space by id", () => {
    const handlers = renderIdentityMenu();
    openMenu();

    fireEvent.click(screen.getByRole("menuitemradio", { name: "Holiday fund" }));

    expect(handlers.onSelectSpace).toHaveBeenCalledWith("holiday-fund");
  });

  it("locks the vault", () => {
    const handlers = renderIdentityMenu();
    openMenu();

    fireEvent.click(screen.getByRole("menuitem", { name: "Lock vault" }));

    expect(handlers.onLock).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("shows the email prefix instead of the display name while locked", () => {
    renderIdentityMenu({ isUnlocked: false });

    expect(screen.getByRole("button", { name: "mohamed, Personal" })).toBeDefined();
    expect(screen.queryByText("Mohamed Halawa")).toBeNull();
  });

  it("closes on Escape and returns focus to the trigger", () => {
    renderIdentityMenu();
    const trigger = screen.getByRole("button", { name: /Mohamed Halawa/ });
    fireEvent.click(trigger);

    fireEvent.keyDown(screen.getByRole("menu"), { key: "Escape" });

    expect(screen.queryByRole("menu")).toBeNull();
    expect(document.activeElement).toBe(trigger);
  });

  it.each([
    ["Manage groups", "onManageGroups"],
    ["Account and passkeys", "onAccountSettings"],
    ["Sign out", "onSignOut"],
  ] as const)("explains that %s arrives with accounts instead of doing nothing", (label, handler) => {
    const handlers = renderIdentityMenu();
    openMenu();

    fireEvent.click(screen.getByRole("menuitem", { name: label }));

    const dialog = screen.getByRole("dialog");

    expect(dialog.textContent).toContain(label);
    expect(dialog.textContent).toContain("arrives with accounts");
    expect(handlers[handler]).toHaveBeenCalledTimes(1);
  });

  it("closes the explanation dialog again", () => {
    renderIdentityMenu();
    openMenu();
    fireEvent.click(screen.getByRole("menuitem", { name: "Sign out" }));

    fireEvent.click(screen.getByRole("button", { name: "Close" }));

    expect(screen.queryByRole("dialog")).toBeNull();
  });
});
