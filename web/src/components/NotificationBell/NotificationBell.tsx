import { ComponentType, Fragment, useState } from "react";
import Badge from "@mui/material/Badge";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Divider from "@mui/material/Divider";
import IconButton from "@mui/material/IconButton";
import Popover from "@mui/material/Popover";
import Typography from "@mui/material/Typography";
import { AlertIcon, BellIcon } from "../../icons/icons";
import { IconProps } from "../../icons/Icon";
import { tokens } from "../../theme/tokens";
import { INotificationResponse } from "../../clients/types";

interface NotificationBellProps {
  notifications: INotificationResponse[];
  unreadCount: number;
  onMarkRead: (id: number) => void;
  onMarkAllRead: () => void;
}

interface NotificationAppearance {
  Icon: ComponentType<IconProps>;
  tone: keyof typeof tokens.chip.light;
  label: string;
}

const appearanceByKind: Record<string, NotificationAppearance> = {
  BudgetExceeded: { Icon: AlertIcon, tone: "overBudget", label: "Budget exceeded" },
};

const fallbackAppearance: NotificationAppearance = {
  Icon: BellIcon,
  tone: "comparison",
  label: "Notification",
};

const describeAppearance = (kind: string): NotificationAppearance =>
  appearanceByKind[kind] ?? fallbackAppearance;

const describeCount = (unreadCount: number): string =>
  unreadCount === 0
    ? "No unread notifications"
    : unreadCount === 1
      ? "1 unread notification"
      : `${unreadCount} unread notifications`;

const NotificationBell = ({
  notifications,
  unreadCount,
  onMarkRead,
  onMarkAllRead,
}: NotificationBellProps) => {
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);

  return (
    <>
      <IconButton
        aria-label={describeCount(unreadCount)}
        onClick={(event) => setAnchor(event.currentTarget)}
      >
        <Badge badgeContent={unreadCount} color="error" overlap="circular">
          <BellIcon size={20} />
        </Badge>
      </IconButton>

      <Popover
        open={anchor !== null}
        anchorEl={anchor}
        onClose={() => setAnchor(null)}
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
        transformOrigin={{ vertical: "top", horizontal: "right" }}
      >
        <Box sx={{ width: 340, padding: 1.5, display: "flex", flexDirection: "column", gap: 1 }}>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <Typography variant="overline">Notifications</Typography>
            {unreadCount > 0 && (
              <Button size="small" onClick={onMarkAllRead}>
                Mark all read
              </Button>
            )}
          </Box>

          {notifications.length === 0 && (
            <Typography variant="body2" color="text.secondary">
              Nothing yet.
            </Typography>
          )}

          {notifications.map((item, index) => {
            const { Icon, tone, label } = describeAppearance(item.kind);

            return (
              <Fragment key={item.id}>
                {index > 0 && <Divider />}
                <Box
                  onClick={() => item.readAt === null && onMarkRead(item.id)}
                  sx={{
                    display: "flex",
                    alignItems: "flex-start",
                    gap: 1.25,
                    padding: 1,
                    borderRadius: 1.5,
                    cursor: item.readAt === null ? "pointer" : "default",
                    backgroundColor: item.readAt === null ? "action.hover" : "transparent",
                  }}
                >
                  <Box
                    sx={{
                      display: "flex",
                      flexShrink: 0,
                      marginTop: 0.25,
                      color: tokens.chip.light[tone].text,
                      "[data-theme='dark'] &": { color: tokens.chip.dark[tone].text },
                    }}
                  >
                    <Icon size={18} title={label} />
                  </Box>

                  <Box sx={{ minWidth: 0 }}>
                    <Typography
                      variant="body1"
                      sx={{ fontWeight: item.readAt === null ? 700 : 400 }}
                    >
                      {item.title}
                    </Typography>
                    <Typography variant="body2" sx={{ color: "text.secondary" }}>
                      {item.message}
                    </Typography>
                  </Box>
                </Box>
              </Fragment>
            );
          })}
        </Box>
      </Popover>
    </>
  );
};

export default NotificationBell;
