import dayjs from "dayjs";

export const ToUnixTimeStamp = (date: string) => {
  return dayjs(date).unix();
};

export const formatDate = (date: number | null) => {
  if (date === null) return null;
  return dayjs.unix(date).format("DD MMM YYYY");
};
