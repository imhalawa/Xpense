export const ToUnixTimeStamp = (date: string) => {
  return Math.floor(new Date(date).getTime() / 1000);
};

export const formatDate = (date: number) => {
  const formatter = Intl.DateTimeFormat("en-US");
  const result = formatter.format(date);
  return result;
};
