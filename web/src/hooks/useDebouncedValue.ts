import { useEffect, useState } from "react";

export const useDebouncedValue = <TValue,>(value: TValue, delayMilliseconds: number): TValue => {
  const [debouncedValue, setDebouncedValue] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedValue(value), delayMilliseconds);
    return () => clearTimeout(timer);
  }, [value, delayMilliseconds]);

  return debouncedValue;
};
