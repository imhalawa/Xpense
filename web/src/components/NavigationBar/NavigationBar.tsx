import { useCallback, useEffect, useState } from "react";
import AppBar from "@mui/material/AppBar";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import LinearProgress from "@mui/material/LinearProgress";
import Toolbar from "@mui/material/Toolbar";
import Typography from "@mui/material/Typography";
import { Link, useLocation } from "react-router";
import { Activity, ArrowLeftRight, Settings } from "lucide-react";
import { useLoading } from "../../contexts/LoadingContext";
import NotificationBell from "../NotificationBell/NotificationBell";
import {
  getUnreadCount,
  listNotifications,
  markAllNotificationsRead,
  markNotificationRead,
} from "../../clients/notifications";
import { INotificationResponse } from "../../clients/types";

const destinations = [
  { path: "/", label: "Overview", Icon: Activity },
  { path: "/transactions", label: "Transactions", Icon: ArrowLeftRight },
  { path: "/settings", label: "Manage", Icon: Settings },
];

const notificationsPage = 1;
const notificationsPageSize = 10;

const NavigationBar = () => {
  const location = useLocation();
  const { loading } = useLoading();
  const [notifications, setNotifications] = useState<INotificationResponse[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);

  const refreshNotifications = useCallback(() => {
    listNotifications(notificationsPage, notificationsPageSize).then((page) =>
      setNotifications(page.notifications)
    );
    getUnreadCount().then(setUnreadCount);
  }, []);

  useEffect(() => {
    refreshNotifications();
  }, [refreshNotifications]);

  return (
    <AppBar position="fixed" sx={{ backgroundColor: "background.paper", color: "text.primary" }}>
      <Toolbar sx={{ height: "100%", gap: 1 }}>
        <Typography variant="h6" component="div" sx={{ marginRight: 2 }}>
          Xpense
        </Typography>

        <Box sx={{ display: "flex", flexGrow: 1, gap: 0.5 }}>
          {destinations.map(({ path, label, Icon }) => (
            <Button
              key={path}
              component={Link}
              to={path}
              color="inherit"
              startIcon={<Icon size={18} />}
              aria-current={location.pathname === path ? "page" : undefined}
              sx={{
                minWidth: "auto",
                backgroundColor: location.pathname === path ? "action.selected" : "transparent",
                "& .MuiButton-startIcon": { marginRight: { xs: 0, sm: 1 } },
              }}>
              <Box component="span" sx={{ display: { xs: "none", sm: "inline" } }}>
                {label}
              </Box>
            </Button>
          ))}
        </Box>

        <NotificationBell
          notifications={notifications}
          unreadCount={unreadCount}
          onMarkRead={(id) => markNotificationRead(id).then(refreshNotifications)}
          onMarkAllRead={() => markAllNotificationsRead().then(refreshNotifications)}
        />
      </Toolbar>
      {loading && <LinearProgress color="primary" sx={{ mt: 0 }} />}
    </AppBar>
  );
};

export default NavigationBar;
