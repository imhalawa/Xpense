import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { ThemeProvider } from "@mui/material/styles";
import theme from "../../theme/theme";
import { INotificationResponse } from "../../clients/types";
import NotificationBell from "./NotificationBell";

const notification = (id: number, readAt: string | null): INotificationResponse => ({
  id,
  kind: "BudgetExceeded",
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
    <ThemeProvider theme={theme}>
      <NotificationBell
        notifications={notifications}
        unreadCount={unreadCount}
        onMarkRead={onMarkRead}
        onMarkAllRead={onMarkAllRead}
      />
    </ThemeProvider>
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
});
