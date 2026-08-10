import { useCallback, useSyncExternalStore } from "react";

export type ColorSchemeMode = "light" | "dark" | "system";
export type ResolvedColorScheme = "light" | "dark";

export const colorSchemeStorageKey = "xpense-color-scheme";

const darkPreferenceQuery = "(prefers-color-scheme: dark)";

const modeListeners = new Set<() => void>();

const readStoredMode = (): ColorSchemeMode => {
  const stored = localStorage.getItem(colorSchemeStorageKey);
  return stored === "light" || stored === "dark" || stored === "system" ? stored : "system";
};

const readSystemPrefersDark = (): boolean => window.matchMedia(darkPreferenceQuery).matches;

const subscribeToMode = (onModeChange: () => void) => {
  modeListeners.add(onModeChange);
  return () => {
    modeListeners.delete(onModeChange);
  };
};

const subscribeToSystemPreference = (onPreferenceChange: () => void) => {
  const preference = window.matchMedia(darkPreferenceQuery);
  preference.addEventListener("change", onPreferenceChange);
  return () => preference.removeEventListener("change", onPreferenceChange);
};

export const useColorScheme = () => {
  const mode = useSyncExternalStore(subscribeToMode, readStoredMode);
  const systemPrefersDark = useSyncExternalStore(
    subscribeToSystemPreference,
    readSystemPrefersDark
  );

  const setMode = useCallback((next: ColorSchemeMode) => {
    localStorage.setItem(colorSchemeStorageKey, next);
    modeListeners.forEach((notifyListener) => notifyListener());
  }, []);

  const resolved: ResolvedColorScheme =
    mode === "system" ? (systemPrefersDark ? "dark" : "light") : mode;

  return { mode, setMode, resolved };
};
