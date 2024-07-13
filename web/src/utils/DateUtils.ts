import dayjs from "dayjs";

export const ToUnixTimeStamp = (date: string) => {
  return dayjs(date).unix();
};

export const formatDate = (date: number) => {
  return dayjs.unix(date).format("DD MMM YYYY HH:mm:ss");
};
