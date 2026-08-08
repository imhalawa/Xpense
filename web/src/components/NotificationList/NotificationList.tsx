import { useState } from "react";
import {
  Body1,
  Button,
  Caption1,
  Checkbox,
  Divider,
  Subtitle2,
  makeStyles,
  mergeClasses,
  tokens,
} from "@fluentui/react-components";
import {
  AlertRegular,
  ChevronLeftRegular,
  ChevronRightRegular,
  WarningRegular,
} from "@fluentui/react-icons";
import { INotificationResponse } from "../../clients/types";

interface NotificationListProps {
  notifications: INotificationResponse[];
  unreadCount: number;
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
  onMarkRead: (id: number) => void;
  onMarkSelectedRead: (ids: number[]) => void;
  onMarkAllRead: () => void;
}

const useStyles = makeStyles({
  root: {
    display: "flex",
    flexDirection: "column",
    minWidth: "340px",
    maxWidth: "420px",
  },
  toolbar: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    gap: tokens.spacingHorizontalM,
    paddingBottom: tokens.spacingVerticalS,
  },
  toolbarActions: {
    display: "flex",
    gap: tokens.spacingHorizontalXS,
  },
  empty: {
    color: tokens.colorNeutralForeground2,
    paddingBlock: tokens.spacingVerticalL,
  },
  item: {
    display: "grid",
    gridTemplateColumns: "auto minmax(0, 1fr)",
    alignItems: "flex-start",
    gap: tokens.spacingHorizontalS,
    paddingBlock: tokens.spacingVerticalS,
  },
  content: {
    display: "grid",
    gridTemplateColumns: "auto minmax(0, 1fr)",
    alignItems: "flex-start",
    gap: tokens.spacingHorizontalS,
    minWidth: 0,
    border: 0,
    backgroundColor: "transparent",
    color: tokens.colorNeutralForeground1,
    textAlign: "left",
    cursor: "pointer",
  },
  read: {
    cursor: "default",
    color: tokens.colorNeutralForeground2,
  },
  icon: {
    color: tokens.colorBrandForeground1,
    marginTop: tokens.spacingVerticalXXS,
  },
  dangerIcon: {
    color: tokens.colorStatusDangerForeground1,
  },
  text: {
    display: "flex",
    flexDirection: "column",
    minWidth: 0,
    gap: tokens.spacingVerticalXXS,
  },
  unreadTitle: {
    fontWeight: tokens.fontWeightSemibold,
  },
  message: {
    color: tokens.colorNeutralForeground2,
  },
  pagination: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    gap: tokens.spacingHorizontalS,
    paddingTop: tokens.spacingVerticalS,
  },
});

const NotificationList = ({
  notifications,
  unreadCount,
  page,
  totalPages,
  onPageChange,
  onMarkRead,
  onMarkSelectedRead,
  onMarkAllRead,
}: NotificationListProps) => {
  const styles = useStyles();
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());

  const toggleSelected = (id: number, checked: boolean) => {
    setSelectedIds((current) => {
      const next = new Set(current);
      if (checked) next.add(id);
      else next.delete(id);
      return next;
    });
  };

  const markSelectedRead = () => {
    onMarkSelectedRead([...selectedIds]);
    setSelectedIds(new Set());
  };

  return (
    <div className={styles.root}>
      <div className={styles.toolbar}>
        <Subtitle2 as="h2">Notifications</Subtitle2>
        <div className={styles.toolbarActions}>
          {selectedIds.size > 0 && (
            <Button size="small" appearance="subtle" onClick={markSelectedRead}>
              Mark selected read
            </Button>
          )}
          {unreadCount > 0 && (
            <Button size="small" appearance="subtle" onClick={onMarkAllRead}>
              Mark all read
            </Button>
          )}
        </div>
      </div>

      {notifications.length === 0 && <Caption1 className={styles.empty}>Nothing yet.</Caption1>}

      {notifications.map((item, index) => {
        const isUnread = item.readAt === null;
        const isBudgetAlert = item.kind === "BudgetExceeded";
        const iconLabel = isBudgetAlert ? "Budget exceeded" : "Notification";

        return (
          <div key={item.id}>
            {index > 0 && <Divider />}
            <div className={styles.item}>
              <Checkbox
                aria-label={`Select ${item.title}`}
                checked={selectedIds.has(item.id)}
                onChange={(_event, data) => toggleSelected(item.id, data.checked === true)}
              />
              <button
                type="button"
                className={mergeClasses(styles.content, !isUnread && styles.read)}
                onClick={() => isUnread && onMarkRead(item.id)}>
                {isBudgetAlert ? (
                  <WarningRegular
                    className={mergeClasses(styles.icon, styles.dangerIcon)}
                    role="img"
                    aria-label={iconLabel}
                  />
                ) : (
                  <AlertRegular className={styles.icon} role="img" aria-label={iconLabel} />
                )}
                <span className={styles.text}>
                  <Body1 className={isUnread ? styles.unreadTitle : undefined}>{item.title}</Body1>
                  <Caption1 className={styles.message}>{item.message}</Caption1>
                </span>
              </button>
            </div>
          </div>
        );
      })}

      {totalPages > 1 && (
        <div className={styles.pagination}>
          <Button
            size="small"
            appearance="subtle"
            icon={<ChevronLeftRegular />}
            aria-label="Previous notifications page"
            disabled={page <= 1}
            onClick={() => onPageChange(page - 1)}
          />
          <Caption1>
            Page {page} of {totalPages}
          </Caption1>
          <Button
            size="small"
            appearance="subtle"
            icon={<ChevronRightRegular />}
            aria-label="Next notifications page"
            disabled={page >= totalPages}
            onClick={() => onPageChange(page + 1)}
          />
        </div>
      )}
    </div>
  );
};

export default NotificationList;
