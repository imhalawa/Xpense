import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { INotificationResponse } from "../../clients/types";
import NotificationBell from "./NotificationBell";

const notification = (
  id: number,
  readAt: string | null,
  kind = "BudgetExceeded"
): INotificationResponse => ({
  id,
  kind,
  title: `Budget exceeded ${id}`,
  message: "Groceries is over budget",
  payload: null,
  readAt,
  createdAt: "2026-08-07T10:00:00Z",
});

const renderBell = (
  notifications: INotificationResponse[],
  unreadCount: number,
  onMarkRead = vi.fn(),
  onMarkAllRead = vi.fn()
) =>
  render(
    <NotificationBell
      notifications={notifications}
      unreadCount={unreadCount}
      onMarkRead={onMarkRead}
      onMarkAllRead={onMarkAllRead}
    />
  );

describe("NotificationBell", () => {
  it("labels the bell with the unread count so it is not colour alone", () => {
    renderBell([notification(1, null)], 1);
    expect(screen.getByRole("button", { name: /1 unread notification/i })).toBeDefined();
  });

  it("uses the plural form for more than one", () => {
    renderBell([notification(1, null), notification(2, null)], 2);
    expect(screen.getByLabelText(/2 unread notifications/i)).toBeDefined();
  });

  it("says there is nothing unread when the count is zero", () => {
    renderBell([], 0);
    expect(screen.getByLabelText(/no unread notifications/i)).toBeDefined();
  });

  it("separates the items with one divider less than the item count", () => {
    const notifications = [notification(1, null), notification(2, null), notification(3, null)];
    renderBell(notifications, 3);
    fireEvent.click(screen.getByLabelText(/3 unread notifications/i));
    expect(screen.getAllByRole("separator").length).toBe(notifications.length - 1);
  });

  it("still shows an icon for a kind it does not know", () => {
    renderBell([notification(1, null, "SomethingNew")], 1);
    fireEvent.click(screen.getByLabelText(/1 unread notification/i));
    expect(screen.getByRole("img", { name: "Notification" })).toBeDefined();
  });
});
