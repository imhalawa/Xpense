import { useCallback, useEffect, useRef, useState } from "react";
import type { ReactElement, ReactNode } from "react";
import { useLocation, useNavigate } from "react-router";
import {
  Button,
  Caption1,
  Hamburger,
  MessageBar,
  MessageBarActions,
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
  DismissRegular,
  HomeRegular,
  ReceiptRegular,
  WalletRegular,
} from "@fluentui/react-icons";
import { signOut } from "../auth/authApi";
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
  AccountDraft,
  SpaceSummary,
  TaxonomyDraft,
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
import type { SidebarResource, SidebarResourceAction } from "./SidebarFilters";
import ResourceDialog from "./ResourceDialog";
import type { ResourceDialogRequest } from "./ResourceDialog";
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
    // NavDrawerHeader lays its children out in a column, which stacked the notification
    // bell under the identity block. State the axis so they sit on one row.
    flexDirection: "row",
    alignItems: "center",
    gap: tokens.spacingHorizontalS,
    paddingBlock: tokens.spacingVerticalM,
    paddingInline: tokens.spacingHorizontalM,
  },
  identityMenu: {
    flexGrow: 1,
    minWidth: 0,
  },
  headerNotifications: {
    flexShrink: 0,
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
  const [accounts, setAccounts] = useState<AccountView[]>([]);
  const [accountsLoaded, setAccountsLoaded] = useState(false);
  const [taxonomy, setTaxonomy] = useState<TaxonomyValue[]>([]);
  const [notifications, setNotifications] = useState<INotificationResponse[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [notificationsPage, setNotificationsPage] = useState(1);
  const [notificationPages, setNotificationPages] = useState(1);
  const [unlockError, setUnlockError] = useState<string | null>(null);
  const [resourceDialog, setResourceDialog] = useState<ResourceDialogRequest | null>(null);
  const [resourceNotice, setResourceNotice] = useState<string | null>(null);
  const [resourceVersion, setResourceVersion] = useState(0);
  const activeDestination = destinationForPath(location.pathname);
  const addTransactionRef = useRef<HTMLButtonElement>(null);
  const usesLegacyNotifications = projection.dataMode === undefined || projection.dataMode === "legacy";

  const refreshNotifications = useCallback(() => {
    if (!usesLegacyNotifications) {
      setNotifications([]);
      setNotificationPages(1);
      setUnreadCount(0);
      return;
    }
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
  }, [notificationsPage, usesLegacyNotifications]);

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
  }, [projection, resourceVersion, state, transactionFilter.filter.space]);

  useEffect(() => {
    if (state !== "unlocked") {
      setAccounts([]);
      setAccountsLoaded(false);
      return;
    }

    let isCurrent = true;
    setAccountsLoaded(false);
    projection
      .listAccounts(transactionFilter.filter.space)
      .then((accounts) => {
        if (!isCurrent) return;
        setAccounts(accounts);
        setAccountsLoaded(true);
      })
      .catch(() => {
        if (!isCurrent) return;
        setAccounts([]);
        setAccountsLoaded(true);
      });

    return () => {
      isCurrent = false;
    };
  }, [projection, resourceVersion, state, transactionFilter.filter.space]);

  const editableAccounts = accounts.filter((account) => account.canEdit);

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

  const toggleAccount = (id: string) => {
    const next = {
      ...transactionFilter.filter,
      account: transactionFilter.filter.account === id ? null : id,
    };
    navigate({ pathname: "/transactions", search: serialiseTransactionFilter(next).toString() });
    closeNavigation();
  };

  const openResourceDialog = (
    action: SidebarResourceAction,
    resource: SidebarResource,
    trigger: HTMLElement,
  ) => setResourceDialog({ action, resource, trigger });

  const saveResource = async (
    request: ResourceDialogRequest,
    draft?: AccountDraft | TaxonomyDraft,
  ) => {
    const { action, resource } = request;
    const isAccount = resource.kind === "account";
    if (action === "delete") {
      if (isAccount) await projection.deleteAccount(transactionFilter.filter.space, resource.value.id);
      else await projection.deleteTaxonomy(transactionFilter.filter.space, resource.kind, resource.value.id);
      const facet = resource.kind === "account" ? "account" : resource.kind;
      if (transactionFilter.filter[facet] === resource.value.id) {
        transactionFilter.setFilter({ ...transactionFilter.filter, [facet]: null });
        setResourceNotice(`The ${facet} filter was cleared because “${resource.value.label}” was deleted.`);
      }
    } else if (isAccount) {
      const accountDraft = draft as AccountDraft;
      if (action === "create") await projection.createAccount(transactionFilter.filter.space, accountDraft);
      else await projection.updateAccount(transactionFilter.filter.space, resource.value.id, accountDraft);
    } else {
      const taxonomyDraft = draft as TaxonomyDraft;
      if (action === "create") await projection.createTaxonomy(transactionFilter.filter.space, resource.kind, taxonomyDraft);
      else await projection.updateTaxonomy(transactionFilter.filter.space, resource.kind, resource.value.id, taxonomyDraft);
    }
    setResourceVersion((version) => version + 1);
  };

  const unlockVault = useCallback(() => {
    setUnlockError(null);
    projection.unlock().catch(() => setUnlockError(unlockFailureMessage));
  }, [projection]);

  // Drop the decrypted projection before the session, so nothing readable outlives the
  // sign-out even if the logout request fails.
  const endSession = useCallback(async () => {
    projection.lock();
    try {
      await signOut();
    } finally {
      await navigate("/signin", { replace: true });
    }
  }, [navigate, projection]);

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
              onSignOut={() => void endSession()}
              onThemeModeChange={setThemeMode}
            />
          </div>
          {usesLegacyNotifications && <div className={styles.headerNotifications}>
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
          </div>}
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
            accounts={accounts}
            values={taxonomy}
            filter={transactionFilter.filter}
            canEdit={spaces.find((space) => space.id === transactionFilter.filter.space)?.canEdit === true}
            onToggleTaxonomy={toggleTaxonomy}
            onToggleAccount={toggleAccount}
            onResourceAction={openResourceDialog}
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
          {resourceNotice !== null && (
            <MessageBar intent="info">
              <MessageBarBody>{resourceNotice}</MessageBarBody>
              <MessageBarActions
                containerAction={
                  <Button
                    appearance="transparent"
                    icon={<DismissRegular />}
                    aria-label="Dismiss resource message"
                    onClick={() => setResourceNotice(null)}
                  />
                }
              />
            </MessageBar>
          )}
          <PageTitle title={activeDestination.label} />
          {children}
        </div>
      </main>
      <TransactionDialog
        accounts={editableAccounts}
        accountsLoaded={accountsLoaded}
        activeSpace={transactionFilter.filter.space}
        returnFocusRef={addTransactionRef}
      />
      <ResourceDialog
        request={resourceDialog}
        onClose={() => setResourceDialog(null)}
        onSubmit={saveResource}
      />
    </div>
  );
};

export default AppShell;
