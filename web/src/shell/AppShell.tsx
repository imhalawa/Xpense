import { ReactNode, useEffect, useMemo, useState } from "react";
import { Link, useLocation } from "react-router";
import {
  Button,
  Divider,
  Drawer,
  DrawerBody,
  DrawerHeader,
  DrawerHeaderTitle,
  InlineDrawer,
  Title2,
} from "@fluentui/react-components";
import {
  HomeRegular,
  ReceiptRegular,
  SettingsRegular,
  WalletRegular,
} from "@fluentui/react-icons";

type Destination = {
  path: string;
  label: string;
  icon: JSX.Element;
};

const destinations: Destination[] = [
  { path: "/", label: "Overview", icon: <HomeRegular /> },
  { path: "/transactions", label: "Transactions", icon: <ReceiptRegular /> },
  { path: "/budgets", label: "Budgets", icon: <WalletRegular /> },
  { path: "/settings", label: "Manage", icon: <SettingsRegular /> },
];

const navWidth = 260;
const wideScreenQuery = "(min-width: 1024px)";

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
  const isWideScreen = useIsWideScreen();
  const location = useLocation();
  const [isNavigationOpen, setIsNavigationOpen] = useState(false);

  useEffect(() => {
    if (isWideScreen) setIsNavigationOpen(false);
  }, [isWideScreen]);

  const navLinks = useMemo(
    () =>
      destinations.map(({ path, label, icon }) => (
        <Link
          key={path}
          to={path}
          aria-current={location.pathname === path ? "page" : undefined}
          onClick={() => setIsNavigationOpen(false)}
          style={{
            borderRadius: 6,
            color: "var(--colorNeutralForeground1)",
            display: "flex",
            alignItems: "center",
            gap: "10px",
            padding: "10px 12px",
            textDecoration: "none",
            backgroundColor:
              location.pathname === path ? "var(--colorNeutralBackground1)" : undefined,
          }}>
          {icon}
          {label}
        </Link>
      )),
    [location.pathname]
  );

  return (
    <div style={{ display: "flex", minHeight: "100vh" }}>
      {isWideScreen ? (
        <InlineDrawer open position="start" size="small" separator>
          <DrawerHeader>
            <DrawerHeaderTitle>
              <Title2>Xpense</Title2>
            </DrawerHeaderTitle>
          </DrawerHeader>
          <Divider inset />
          <DrawerBody>{navLinks}</DrawerBody>
        </InlineDrawer>
      ) : (
        <>
          <Button
            appearance="subtle"
            onClick={() => setIsNavigationOpen(true)}
            style={{ position: "fixed", insetBlockStart: 12, insetInlineStart: 12, zIndex: 2 }}>
            Menu
          </Button>
          <Drawer
            type="overlay"
            position="start"
            open={isNavigationOpen}
            onOpenChange={(_event, data) => setIsNavigationOpen(Boolean(data.open))}
            style={{ width: navWidth, position: "fixed", top: 0, left: 0 }}>
            <DrawerHeader>
              <DrawerHeaderTitle>
                <Title2>Xpense</Title2>
              </DrawerHeaderTitle>
            </DrawerHeader>
            <Divider inset />
            <DrawerBody>{navLinks}</DrawerBody>
          </Drawer>
        </>
      )}

      <main
        style={{
          flex: 1,
          padding: "24px",
          marginLeft: isWideScreen ? navWidth : 0,
        }}>
        <div style={{ maxWidth: 1120, marginInline: "auto" }}>{children}</div>
      </main>
    </div>
  );
};

export default AppShell;
