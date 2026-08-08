import { ReactElement, ReactNode, useCallback, useEffect, useState } from "react";
import { useLocation, useNavigate } from "react-router";
import {
  Hamburger,
  NavDrawer,
  NavDrawerBody,
  NavDrawerFooter,
  NavDrawerHeader,
  NavItem,
  ProgressBar,
  Subtitle1,
  makeStyles,
  mergeClasses,
  tokens,
} from "@fluentui/react-components";
import {
  HomeRegular,
  ReceiptRegular,
  SettingsRegular,
  WalletRegular,
} from "@fluentui/react-icons";
import ThemeModeToggle from "../components/ThemeModeToggle/ThemeModeToggle";
import NotificationBell from "../components/NotificationBell/NotificationBell";
import { useLoading } from "../contexts/LoadingContext";
import {
  getUnreadCount,
  listNotifications,
  markAllNotificationsRead,
  markNotificationRead,
} from "../clients/notifications";
import { INotificationResponse } from "../clients/types";

type Destination = {
  path: string;
  label: string;
  icon: ReactElement;
};

const destinations: Destination[] = [
  { path: "/", label: "Overview", icon: <HomeRegular /> },
  { path: "/transactions", label: "Transactions", icon: <ReceiptRegular /> },
  { path: "/budgets", label: "Budgets", icon: <WalletRegular /> },
  { path: "/settings", label: "Manage", icon: <SettingsRegular /> },
];

const wideScreenQuery = "(min-width: 1024px)";
const notificationsPageSize = 10;

const useStyles = makeStyles({
  root: {
    display: "flex",
    minHeight: "100vh",
    backgroundColor: tokens.colorNeutralBackground2,
  },
  drawer: {
    flexShrink: 0,
    height: "100vh",
    position: "sticky",
    top: 0,
    backgroundColor: tokens.colorBrandBackground2,
    borderInlineEndWidth: tokens.strokeWidthThin,
    borderInlineEndStyle: "solid",
    borderInlineEndColor: tokens.colorBrandStroke2,
  },
  header: {
    paddingTop: tokens.spacingVerticalXXL,
  },
  brand: {
    paddingInline: tokens.spacingHorizontalM,
    color: tokens.colorBrandForeground1,
  },
  navItem: {
    backgroundColor: "transparent",
  },
  footer: {
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalS,
    borderTop: `${tokens.strokeWidthThin} solid ${tokens.colorBrandStroke2}`,
    paddingTop: tokens.spacingVerticalM,
  },
  menuButton: {
    position: "fixed",
    insetBlockStart: tokens.spacingVerticalM,
    insetInlineStart: tokens.spacingHorizontalM,
    zIndex: 10,
  },
  main: {
    boxSizing: "border-box",
    flexGrow: 1,
    minWidth: 0,
    padding: tokens.spacingHorizontalXXL,
  },
  mobileMain: {
    paddingTop: "64px",
  },
  content: {
    width: "100%",
    maxWidth: "1120px",
    marginInline: "auto",
  },
  loading: {
    position: "fixed",
    insetBlockStart: 0,
    insetInline: 0,
    zIndex: 20,
  },
});

const useIsWideScreen = () => {
  const evaluate = () => window.matchMedia(wideScreenQuery).matches;
  const [isWideScreen, setIsWideScreen] = useState<boolean>(evaluate);

  useEffect(() => {
    const mediaQuery = window.matchMedia(wideScreenQuery);
    const handleChange = () => setIsWideScreen(mediaQuery.matches);

    handleChange();
    mediaQuery.addEventListener("change", handleChange);
    return () => mediaQuery.removeEventListener("change", handleChange);
  }, []);

  return isWideScreen;
};

interface AppShellProps {
  children: ReactNode;
}

const AppShell = ({ children }: AppShellProps) => {
  const styles = useStyles();
  const isWideScreen = useIsWideScreen();
  const location = useLocation();
  const navigate = useNavigate();
  const { loading } = useLoading();
  const [isNavigationOpen, setIsNavigationOpen] = useState(false);
  const [notifications, setNotifications] = useState<INotificationResponse[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [notificationsPage, setNotificationsPage] = useState(1);
  const [notificationPages, setNotificationPages] = useState(1);

  const refreshNotifications = useCallback(() => {
    Promise.all([
      listNotifications(notificationsPage, notificationsPageSize),
      getUnreadCount(),
    ])
      .then(([response, count]) => {
        setNotifications(response.notifications);
        setNotificationPages(response.totalPages);
        setUnreadCount(count);
      })
      .catch((loadError) => console.error(loadError));
  }, [notificationsPage]);

  useEffect(() => {
    refreshNotifications();
  }, [refreshNotifications]);

  useEffect(() => {
    if (isWideScreen) setIsNavigationOpen(false);
  }, [isWideScreen]);

  const navigateTo = (path: string) => (event: React.MouseEvent<HTMLAnchorElement>) => {
    event.preventDefault();
    navigate(path);
    setIsNavigationOpen(false);
  };

  return (
    <div className={styles.root}>
      {loading && <ProgressBar className={styles.loading} />}
      {!isWideScreen && (
        <Hamburger
          className={styles.menuButton}
          aria-label="Open navigation"
          onClick={() => setIsNavigationOpen(true)}
        />
      )}

      <NavDrawer
        className={styles.drawer}
        type={isWideScreen ? "inline" : "overlay"}
        open={isWideScreen || isNavigationOpen}
        selectedValue={location.pathname}
        onOpenChange={(_event, data) => setIsNavigationOpen(Boolean(data.open))}>
        <NavDrawerHeader className={styles.header}>
          <Subtitle1 as="span" className={styles.brand}>
            Xpense
          </Subtitle1>
        </NavDrawerHeader>
        <NavDrawerBody>
          {destinations.map(({ path, label, icon }) => (
            <NavItem
              key={path}
              as="a"
              href={path}
              value={path}
              className={styles.navItem}
              icon={icon}
              aria-current={location.pathname === path ? "page" : undefined}
              onClick={navigateTo(path)}>
              {label}
            </NavItem>
          ))}
        </NavDrawerBody>
        <NavDrawerFooter className={styles.footer}>
          <NotificationBell
            notifications={notifications}
            unreadCount={unreadCount}
            page={notificationsPage}
            totalPages={notificationPages}
            onPageChange={setNotificationsPage}
            onMarkRead={(id) => markNotificationRead(id).then(refreshNotifications)}
            onMarkSelectedRead={(ids) =>
              Promise.all(ids.map(markNotificationRead)).then(refreshNotifications)
            }
            onMarkAllRead={() => markAllNotificationsRead().then(refreshNotifications)}
          />
          <ThemeModeToggle />
        </NavDrawerFooter>
      </NavDrawer>

      <main className={mergeClasses(styles.main, !isWideScreen && styles.mobileMain)}>
        <div className={styles.content}>{children}</div>
      </main>
    </div>
  );
};

export default AppShell;
