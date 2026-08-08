import {
  Button,
  CounterBadge,
  Popover,
  PopoverSurface,
  PopoverTrigger,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { AlertRegular } from "@fluentui/react-icons";
import { INotificationResponse } from "../../clients/types";
import NotificationList from "../NotificationList/NotificationList";

interface NotificationBellProps {
  notifications: INotificationResponse[];
  unreadCount: number;
  page?: number;
  totalPages?: number;
  onPageChange?: (page: number) => void;
  onMarkRead: (id: number) => void;
  onMarkSelectedRead?: (ids: number[]) => void;
  onMarkAllRead: () => void;
}

const useStyles = makeStyles({
  trigger: {
    position: "relative",
  },
  badge: {
    position: "absolute",
    insetBlockStart: "-6px",
    insetInlineEnd: "-6px",
  },
  surface: {
    padding: tokens.spacingHorizontalL,
    borderTop: `${tokens.strokeWidthThick} solid ${tokens.colorBrandStroke1}`,
  },
});

const describeCount = (unreadCount: number): string =>
  unreadCount === 0
    ? "No unread notifications"
    : unreadCount === 1
      ? "1 unread notification"
      : `${unreadCount} unread notifications`;

const NotificationBell = ({
  notifications,
  unreadCount,
  page = 1,
  totalPages = 1,
  onPageChange = () => undefined,
  onMarkRead,
  onMarkSelectedRead = () => undefined,
  onMarkAllRead,
}: NotificationBellProps) => {
  const styles = useStyles();

  return (
    <Popover positioning="above-start">
      <PopoverTrigger disableButtonEnhancement>
        <Button
          className={styles.trigger}
          appearance="subtle"
          icon={<AlertRegular />}
          aria-label={describeCount(unreadCount)}>
          {unreadCount > 0 && (
            <CounterBadge className={styles.badge} count={unreadCount} color="danger" size="small" />
          )}
        </Button>
      </PopoverTrigger>
      <PopoverSurface className={styles.surface} aria-label="Notifications">
        <NotificationList
          notifications={notifications}
          unreadCount={unreadCount}
          page={page}
          totalPages={totalPages}
          onPageChange={onPageChange}
          onMarkRead={onMarkRead}
          onMarkSelectedRead={onMarkSelectedRead}
          onMarkAllRead={onMarkAllRead}
        />
      </PopoverSurface>
    </Popover>
  );
};

export default NotificationBell;
