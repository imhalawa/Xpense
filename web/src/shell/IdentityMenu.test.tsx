import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { tokens } from "@fluentui/react-components";
import { useState } from "react";
import type { SpaceSummary } from "../vault/VaultProjection";
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
    onUnlock: vi.fn(),
    onSignOut: vi.fn(),
    onThemeModeChange: vi.fn(),
  };

  render(
    <IdentityMenu
      spaces={spaces}
      activeSpace="personal"
      displayName="Mohamed Halawa"
      emailPrefix="mohamed"
      isUnlocked
      themeMode="system"
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

  it("switches from lock to unlock without leaving the lock action active", () => {
    const onLock = vi.fn();
    const onUnlock = vi.fn();
    const Harness = () => {
      const [isUnlocked, setIsUnlocked] = useState(true);
      return (
        <IdentityMenu
          spaces={spaces}
          activeSpace="personal"
          displayName="Mohamed Halawa"
          emailPrefix="mohamed"
          isUnlocked={isUnlocked}
          themeMode="system"
          onSelectSpace={vi.fn()}
          onLock={() => {
            onLock();
            setIsUnlocked(false);
          }}
          onUnlock={() => {
            onUnlock();
            setIsUnlocked(true);
          }}
          onThemeModeChange={vi.fn()}
        />
      );
    };

    render(<Harness />);
    openMenu();
    fireEvent.click(screen.getByRole("menuitem", { name: "Lock vault" }));
    fireEvent.click(screen.getByRole("button", { name: "mohamed, Personal" }));

    expect(screen.queryByRole("menuitem", { name: "Lock vault" })).toBeNull();
    fireEvent.click(screen.getByRole("menuitem", { name: "Unlock vault" }));

    expect(onLock).toHaveBeenCalledTimes(1);
    expect(onUnlock).toHaveBeenCalledTimes(1);
    expect(screen.getByRole("button", { name: "Mohamed Halawa, Personal" })).toBeDefined();
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
  ] as const)("reports %s directly without showing a placeholder", (label, handler) => {
    const handlers = renderIdentityMenu();
    openMenu();

    fireEvent.click(screen.getByRole("menuitem", { name: label }));

    expect(handlers[handler]).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("omits account actions whose callbacks are unavailable", () => {
    renderIdentityMenu({
      onManageGroups: undefined,
      onAccountSettings: undefined,
      onSignOut: undefined,
    });
    openMenu();

    expect(screen.queryByRole("menuitem", { name: "Manage groups" })).toBeNull();
    expect(screen.queryByRole("menuitem", { name: "Account and passkeys" })).toBeNull();
    expect(screen.queryByRole("menuitem", { name: "Sign out" })).toBeNull();
  });

  it("changes theme from an accessible submenu", () => {
    const handlers = renderIdentityMenu({ themeMode: "system" });
    openMenu();
    fireEvent.click(screen.getByRole("menuitem", { name: "Theme" }));

    const themeChoices = screen.getAllByRole("menuitemradio").slice(-3);
    expect(themeChoices.map((item) => item.textContent)).toEqual(["System", "Light", "Dark"]);
    expect(screen.getByRole("menuitemradio", { name: "System" }).getAttribute("aria-checked")).toBe(
      "true",
    );

    fireEvent.click(screen.getByRole("menuitemradio", { name: "Dark" }));

    expect(handlers.onThemeModeChange).toHaveBeenCalledWith("dark");
  });

  it("enters, selects and exits the theme submenu with the keyboard", async () => {
    const handlers = renderIdentityMenu({ themeMode: "system" });
    const trigger = screen.getByRole("button", { name: "Mohamed Halawa, Personal" });
    trigger.focus();
    fireEvent.keyDown(trigger, { key: "ArrowDown" });

    const theme = await screen.findByRole("menuitem", { name: "Theme" });
    theme.focus();
    fireEvent.keyDown(theme, { key: "ArrowRight" });

    const system = await screen.findByRole("menuitemradio", { name: "System" });
    await waitFor(() => expect(document.activeElement).toBe(system));
    fireEvent.keyDown(system, { key: "Enter" });

    expect(handlers.onThemeModeChange).toHaveBeenCalledWith("system");
    await waitFor(() => expect(document.activeElement).toBe(trigger));

    fireEvent.keyDown(trigger, { key: "ArrowDown" });
    const reopenedTheme = await screen.findByRole("menuitem", { name: "Theme" });
    reopenedTheme.focus();
    fireEvent.keyDown(reopenedTheme, { key: "ArrowRight" });
    const reopenedSystem = await screen.findByRole("menuitemradio", { name: "System" });
    await waitFor(() => expect(document.activeElement).toBe(reopenedSystem));
    fireEvent.keyDown(reopenedSystem, { key: "Escape" });

    await waitFor(() => expect(document.activeElement).toBe(reopenedTheme));
    fireEvent.keyDown(reopenedTheme, { key: "Escape" });
    await waitFor(() => expect(document.activeElement).toBe(trigger));
    expect(screen.queryByRole("menu")).toBeNull();
  });

  it("uses the Fluent eight pixel spacing token between avatar and identity text", () => {
    renderIdentityMenu();

    const trigger = screen.getByRole("button", { name: "Mohamed Halawa, Personal" });
    expect(getComputedStyle(trigger).columnGap).toBe(tokens.spacingHorizontalS);
  });
});
