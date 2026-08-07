import axios from "axios";
import { INotificationPageResponse, IUnreadCountResponse } from "./types";

export const listNotifications = async (
  page: number,
  pageSize: number
): Promise<INotificationPageResponse> => {
  const response = await axios.get<INotificationPageResponse>("/api/v1/notifications", {
    params: { page, pageSize },
  });
  return response.data;
};

export const getUnreadCount = async (): Promise<number> => {
  const response = await axios.get<IUnreadCountResponse>("/api/v1/notifications/unread-count");
  return response.data.unread;
};

export const markNotificationRead = async (id: number): Promise<void> => {
  await axios.patch(`/api/v1/notifications/${id}/read`);
};

export const markAllNotificationsRead = async (): Promise<void> => {
  await axios.post("/api/v1/notifications/read-all");
};
