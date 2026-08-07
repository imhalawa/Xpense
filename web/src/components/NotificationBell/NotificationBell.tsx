import { useState } from "react";
import Badge from "@mui/material/Badge";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import IconButton from "@mui/material/IconButton";
import Popover from "@mui/material/Popover";
import Typography from "@mui/material/Typography";
import { Bell } from "lucide-react";
import { INotificationResponse } from "../../clients/types";

interface NotificationBellProps {
  notifications: INotificationResponse[];
  unreadCount: number;
  onMarkRead: (id: number) => void;
  onMarkAllRead: () => void;
}

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
          <Bell size={20} />
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

          {notifications.map((item) => (
            <Box
              key={item.id}
              onClick={() => item.readAt === null && onMarkRead(item.id)}
              sx={{
                padding: 1,
                borderRadius: 1.5,
                cursor: item.readAt === null ? "pointer" : "default",
                backgroundColor: item.readAt === null ? "action.hover" : "transparent",
              }}
            >
              <Typography variant="body1" sx={{ fontWeight: item.readAt === null ? 700 : 400 }}>
                {item.title}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {item.message}
              </Typography>
            </Box>
          ))}
        </Box>
      </Popover>
    </>
  );
};

export default NotificationBell;
