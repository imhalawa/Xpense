import { act, renderHook } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { colorSchemeStorageKey, useColorScheme } from "./useColorScheme";

const stubSystemPreference = (prefersDark: boolean) => {
  window.matchMedia = vi.fn().mockImplementation((query: string) => ({
    matches: prefersDark,
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  }));
};

afterEach(() => {
  localStorage.clear();
});

describe("useColorScheme", () => {
  it("defaults to system when nothing is stored", () => {
    const { result } = renderHook(() => useColorScheme());
    expect(result.current.mode).toBe("system");
  });

  it("persists the chosen mode", () => {
    const { result } = renderHook(() => useColorScheme());

    act(() => result.current.setMode("dark"));

    expect(result.current.mode).toBe("dark");
    expect(localStorage.getItem(colorSchemeStorageKey)).toBe("dark");
  });

  it("reads a stored mode on init", () => {
    localStorage.setItem(colorSchemeStorageKey, "light");
    const { result } = renderHook(() => useColorScheme());
    expect(result.current.mode).toBe("light");
  });

  it("resolves an explicit mode without asking the system", () => {
    stubSystemPreference(true);
    localStorage.setItem(colorSchemeStorageKey, "light");

    const { result } = renderHook(() => useColorScheme());

    expect(result.current.resolved).toBe("light");
  });

  it("resolves system to the operating system preference", () => {
    stubSystemPreference(true);

    const { result } = renderHook(() => useColorScheme());

    expect(result.current.resolved).toBe("dark");
  });
});
