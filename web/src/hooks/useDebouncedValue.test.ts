import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useDebouncedValue } from "./useDebouncedValue";

describe("useDebouncedValue", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("hands back the first value straight away", () => {
    const { result } = renderHook(() => useDebouncedValue("albert", 250));

    expect(result.current).toBe("albert");
  });

  it("holds a new value back until the delay has passed", () => {
    const { result, rerender } = renderHook(({ value }) => useDebouncedValue(value, 250), {
      initialProps: { value: "al" },
    });

    rerender({ value: "albert" });
    expect(result.current).toBe("al");

    act(() => {
      vi.advanceTimersByTime(249);
    });
    expect(result.current).toBe("al");

    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(result.current).toBe("albert");
  });

  it("skips the values typed in between and settles on the last one", () => {
    const { result, rerender } = renderHook(({ value }) => useDebouncedValue(value, 250), {
      initialProps: { value: "a" },
    });

    rerender({ value: "al" });
    act(() => {
      vi.advanceTimersByTime(100);
    });
    rerender({ value: "alb" });
    act(() => {
      vi.advanceTimersByTime(100);
    });
    rerender({ value: "albert" });
    act(() => {
      vi.advanceTimersByTime(250);
    });

    expect(result.current).toBe("albert");
  });

  it("drops the pending timer when the component goes away", () => {
    const clearTimer = vi.spyOn(globalThis, "clearTimeout");
    const { rerender, unmount } = renderHook(({ value }) => useDebouncedValue(value, 250), {
      initialProps: { value: "al" },
    });

    rerender({ value: "albert" });
    unmount();

    expect(clearTimer).toHaveBeenCalled();
    expect(vi.getTimerCount()).toBe(0);
    clearTimer.mockRestore();
  });
});
