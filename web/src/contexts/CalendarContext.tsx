import dayjs, { Dayjs } from "dayjs";
import { createContext, useContext, useState } from "react";

interface ICalendarContext {
  selectedDate: Dayjs | null;
  setSelectedDate: React.Dispatch<React.SetStateAction<Dayjs | null>>;
}

const CalendarContext = createContext({} as ICalendarContext);

export const CalendarContextProvider = ({ children }: any) => {
  const [selectedDate, setSelectedDate] = useState<Dayjs | null>(null);
  return <CalendarContext.Provider value={{ selectedDate, setSelectedDate }}>{children}</CalendarContext.Provider>;
};

export const useCalendar = () => useContext(CalendarContext);
