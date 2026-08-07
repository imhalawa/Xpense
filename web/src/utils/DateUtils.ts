import dayjs from "dayjs";

export const formatIsoDate = (date: string | null) => {
  if (date === null) return null;
  return dayjs(date).format("DD MMM YYYY");
};
