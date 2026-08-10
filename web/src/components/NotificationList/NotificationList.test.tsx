import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { INotificationResponse } from "../../clients/types";
import NotificationList from "./NotificationList";

const notification = (id: number, readAt: string | null): INotificationResponse => ({
  id,
  kind: "BudgetExceeded",
  title: `Budget exceeded ${id}`,
  message: "Groceries is over budget",
  payload: null,
  readAt,
  createdAt: "2026-08-07T10:00:00Z",
});

const renderList = (
  onMarkSelectedRead = vi.fn(),
  notifications = [notification(1, null), notification(2, null)]
) =>
  render(
    <NotificationList
      notifications={notifications}
      unreadCount={2}
      page={1}
      totalPages={2}
      onPageChange={vi.fn()}
      onMarkRead={vi.fn()}
      onMarkSelectedRead={onMarkSelectedRead}
      onMarkAllRead={vi.fn()}
    />
  );

describe("NotificationList", () => {
  it("marks selected notifications read", () => {
    const onMarkSelectedRead = vi.fn();
    renderList(onMarkSelectedRead);
    fireEvent.click(screen.getByRole("checkbox", { name: "Select Budget exceeded 1" }));
    fireEvent.click(screen.getByRole("button", { name: "Mark selected read" }));
    expect(onMarkSelectedRead).toHaveBeenCalledWith([1]);
  });

  it("separates every row except the last", () => {
    renderList(vi.fn(), [
      notification(1, null),
      notification(2, "2026-08-08T10:00:00Z"),
      notification(3, null),
    ]);
    const rows = screen
      .getAllByRole("button", { name: /Groceries is over budget/ })
      .map((button) => button.parentElement as HTMLElement);
    const borders = rows.map((row) => window.getComputedStyle(row).borderBottomStyle);
    expect(borders).toEqual(["solid", "solid", ""]);
  });

  it("supports paging", () => {
    renderList();
    expect(screen.getByRole("button", { name: "Next notifications page" })).toBeDefined();
  });
});
