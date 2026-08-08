import { useCallback, useEffect, useRef, useState } from "react";
import type { ReactElement, ReactNode } from "react";
import { useLocation, useNavigate } from "react-router";
import {
  Button,
  Caption1,
  Hamburger,
  MessageBar,
  MessageBarBody,
  NavDrawer,
  NavDrawerBody,
  NavDrawerHeader,
  NavItem,
  ProgressBar,
  makeStyles,
  mergeClasses,
  tokens,
} from "@fluentui/react-components";
import {
  AddRegular,
  HomeRegular,
  ReceiptRegular,
  SettingsRegular,
  WalletRegular,
} from "@fluentui/react-icons";
import NotificationBell from "../components/NotificationBell/NotificationBell";
import { useLoading } from "../contexts/LoadingContext";
import {
  getUnreadCount,
  listNotifications,
  markAllNotificationsRead,
  markNotificationRead,
} from "../clients/notifications";
import type { INotificationResponse } from "../clients/types";
import { useColorScheme } from "../fluent/useColorScheme";
import { useVault } from "../vault/VaultProvider";
import type {
  AccountView,
  SpaceSummary,
  TaxonomyKind,
  TaxonomyValue,
} from "../vault/VaultProjection";
import {
  clearTransactionFilters,
  serialiseTransactionFilter,
  toggleTaxonomyFilter,
} from "../transactions/transactionFilterState";
import { useTransactionFilter } from "../transactions/useTransactionFilter";
import IdentityMenu from "./IdentityMenu";
import PageTitle from "./PageTitle";
import SidebarFilters from "./SidebarFilters";
import TransactionDialog from "./TransactionDialog";
import { useIsWideScreen } from "./useIsWideScreen";

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

const fallbackSpace = "personal";
const localDisplayName = "Local user";
const localEmailPrefix = "local";
const notificationsPageSize = 10;
const taxonomyKinds: TaxonomyKind[] = ["category", "tag", "merchant"];
const unlockFailureMessage = "The vault could not be unlocked. Try again.";

const useStyles = makeStyles({
  root: {
    display: "flex",
    minHeight: "100vh",
    backgroundColor: tokens.colorNeutralBackground1,
  },
  drawer: {
    flexShrink: 0,
    width: "280px",
    minWidth: "280px",
    maxWidth: "280px",
    height: "100vh",
    position: "sticky",
    top: 0,
    backgroundColor: tokens.colorNeutralBackground2,
    borderInlineEndWidth: tokens.strokeWidthThin,
    borderInlineEndStyle: "solid",
    borderInlineEndColor: tokens.colorNeutralStroke2,
  },
  header: {
    display: "flex",
    alignItems: "center",
    gap: tokens.spacingHorizontalS,
    paddingBlock: tokens.spacingVerticalM,
    paddingInline: tokens.spacingHorizontalM,
  },
  identityMenu: {
    flexGrow: 1,
    minWidth: 0,
  },
  body: {
    display: "flex",
    flexDirection: "column",
    paddingInline: tokens.spacingHorizontalM,
  },
  addTransaction: {
    width: "100%",
    justifyContent: "flex-start",
    marginBlockEnd: tokens.spacingVerticalM,
  },
  addTransactionExplanation: {
    display: "block",
    color: tokens.colorNeutralForeground3,
    marginBlockStart: `-${tokens.spacingVerticalS}`,
    marginBlockEnd: tokens.spacingVerticalM,
  },
  navigation: {
    display: "flex",
    flexDirection: "column",
  },
  navItem: {
    backgroundColor: "transparent",
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
    padding: tokens.spacingHorizontalXXXL,
  },
  mobileMain: {
    paddingTop: "64px",
  },
  content: {
    width: "100%",
    maxWidth: "1200px",
    marginInline: "auto",
  },
  loading: {
    position: "fixed",
    insetBlockStart: 0,
    insetInline: 0,
    zIndex: 20,
  },
});

const destinationForPath = (pathname: string): Destination =>
  destinations.find((destination) =>
    destination.path === "/"
      ? pathname === "/"
      : pathname === destination.path || pathname.startsWith(`${destination.path}/`),
  ) ?? destinations[0];

interface AppShellProps {
  children: ReactNode;
}

const AppShell = ({ children }: AppShellProps) => {
  const styles = useStyles();
  const isWideScreen = useIsWideScreen();
  const location = useLocation();
  const navigate = useNavigate();
  const { loading } = useLoading();
  const { projection, state } = useVault();
  const { mode: themeMode, setMode: setThemeMode } = useColorScheme();
  const transactionFilter = useTransactionFilter(projection, fallbackSpace);
  const [isNavigationOpen, setIsNavigationOpen] = useState(false);
  const [spaces, setSpaces] = useState<SpaceSummary[]>([]);
  const [editableAccounts, setEditableAccounts] = useState<AccountView[]>([]);
  const [accountsLoaded, setAccountsLoaded] = useState(false);
  const [taxonomy, setTaxonomy] = useState<TaxonomyValue[]>([]);
  const [notifications, setNotifications] = useState<INotificationResponse[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [notificationsPage, setNotificationsPage] = useState(1);
  const [notificationPages, setNotificationPages] = useState(1);
  const [unlockError, setUnlockError] = useState<string | null>(null);
  const activeDestination = destinationForPath(location.pathname);
  const addTransactionRef = useRef<HTMLButtonElement>(null);

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

  useEffect(() => {
    if (state !== "unlocked") {
      setSpaces([]);
      setTaxonomy([]);
      return;
    }

    let isCurrent = true;
    Promise.all([
      projection.listSpaces(),
      ...taxonomyKinds.map((kind) => projection.listTaxonomy(transactionFilter.filter.space, kind)),
    ])
      .then(([loadedSpaces, ...taxonomyGroups]) => {
        if (!isCurrent) return;
        setSpaces(loadedSpaces);
        setTaxonomy(taxonomyGroups.flat());
      })
      .catch(() => {
        if (!isCurrent) return;
        setSpaces([]);
        setTaxonomy([]);
      });

    return () => {
      isCurrent = false;
    };
  }, [projection, state, transactionFilter.filter.space]);

  useEffect(() => {
    if (state !== "unlocked") {
      setEditableAccounts([]);
      setAccountsLoaded(false);
      return;
    }

    let isCurrent = true;
    setAccountsLoaded(false);
    projection
      .listAccounts(transactionFilter.filter.space)
      .then((accounts) => {
        if (!isCurrent) return;
        setEditableAccounts(accounts.filter((account) => account.canEdit));
        setAccountsLoaded(true);
      })
      .catch(() => {
        if (!isCurrent) return;
        setEditableAccounts([]);
        setAccountsLoaded(true);
      });

    return () => {
      isCurrent = false;
    };
  }, [projection, state, transactionFilter.filter.space]);

  const closeNavigation = () => setIsNavigationOpen(false);

  const navigateTo = (path: string) => (event: React.MouseEvent<HTMLAnchorElement>) => {
    event.preventDefault();
    const search = serialiseTransactionFilter(transactionFilter.filter).toString();
    navigate({ pathname: path, search });
    closeNavigation();
  };

  const selectSpace = (space: string) => {
    const next = { ...clearTransactionFilters(transactionFilter.filter), space };
    transactionFilter.setFilter(next);
    closeNavigation();
  };

  const toggleTaxonomy = (kind: TaxonomyKind, id: string) => {
    const next = toggleTaxonomyFilter(transactionFilter.filter, kind, id);
    navigate({ pathname: "/transactions", search: serialiseTransactionFilter(next).toString() });
    closeNavigation();
  };

  const unlockVault = useCallback(() => {
    setUnlockError(null);
    projection.unlock().catch(() => setUnlockError(unlockFailureMessage));
  }, [projection]);

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
        role="presentation"
        type={isWideScreen ? "inline" : "overlay"}
        open={isWideScreen || isNavigationOpen}
        selectedValue={activeDestination.path}
        onOpenChange={(_event, data) => setIsNavigationOpen(Boolean(data.open))}>
        <NavDrawerHeader className={styles.header}>
          <div className={styles.identityMenu}>
            <IdentityMenu
              spaces={spaces}
              activeSpace={transactionFilter.filter.space}
              displayName={localDisplayName}
              emailPrefix={localEmailPrefix}
              isUnlocked={state === "unlocked"}
              themeMode={themeMode}
              onSelectSpace={selectSpace}
              onLock={() => projection.lock()}
              onUnlock={unlockVault}
              onThemeModeChange={setThemeMode}
            />
          </div>
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
        </NavDrawerHeader>
        <NavDrawerBody className={styles.body}>
          <Button
            ref={addTransactionRef}
            className={styles.addTransaction}
            appearance="primary"
            icon={<AddRegular />}
            disabled={!accountsLoaded || editableAccounts.length === 0}
            aria-describedby={
              accountsLoaded && editableAccounts.length === 0
                ? "add-transaction-description"
                : undefined
            }
            title={
              accountsLoaded && editableAccounts.length === 0
                ? "No account in this space can be edited."
                : undefined
            }
            onClick={() => {
              navigate("/transactions/new", {
                state: { returnTo: `${location.pathname}${location.search}` },
              });
              closeNavigation();
            }}>
            Add transaction
          </Button>
          {accountsLoaded && editableAccounts.length === 0 && (
            <Caption1
              id="add-transaction-description"
              className={styles.addTransactionExplanation}>
              No account in this space can be edited.
            </Caption1>
          )}

          <nav className={styles.navigation} aria-label="Primary">
            {destinations.map(({ path, label, icon }) => (
              <NavItem
                key={path}
                as="a"
                href={path}
                value={path}
                className={styles.navItem}
                icon={icon}
                aria-current={activeDestination.path === path ? "page" : undefined}
                onClick={navigateTo(path)}>
                {label}
              </NavItem>
            ))}
          </nav>

          <SidebarFilters
            values={taxonomy}
            filter={transactionFilter.filter}
            onToggleTaxonomy={toggleTaxonomy}
          />
        </NavDrawerBody>
      </NavDrawer>

      <main className={mergeClasses(styles.main, !isWideScreen && styles.mobileMain)}>
        <div className={styles.content} data-testid="app-content">
          {unlockError !== null && (
            <MessageBar intent="error" role="alert">
              <MessageBarBody>{unlockError}</MessageBarBody>
            </MessageBar>
          )}
          <PageTitle title={activeDestination.label} />
          {children}
        </div>
      </main>
      <TransactionDialog
        accounts={editableAccounts}
        activeSpace={transactionFilter.filter.space}
        returnFocusRef={addTransactionRef}
      />
    </div>
  );
};

export default AppShell;
