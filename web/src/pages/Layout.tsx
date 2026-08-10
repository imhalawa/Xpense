import { Outlet } from "react-router";
import AppShell from "../shell/AppShell.tsx";

const Layout = () => {
  return (
    <AppShell>
      <Outlet />
    </AppShell>
  );
};

export default Layout;
