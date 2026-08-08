import "./App.css";

import { useEffect, useMemo } from "react";
import { Route, Routes } from "react-router";
import { FluentProvider, makeStyles } from "@fluentui/react-components";
import Overview from "./pages/Overview/Overview.tsx";
import Transactions from "./pages/Transactions/Transactions.tsx";
import Budgets from "./pages/Budgets/Budgets.tsx";
import Settings from "./pages/Settings/Settings.tsx";
import Layout from "./pages/Layout.tsx";
import { darkTheme, lightTheme } from "./fluent/theme.ts";
import { useColorScheme } from "./fluent/useColorScheme.ts";
import GlobalStyles from "./fluent/GlobalStyles.tsx";
import { LoadingContextProvider } from "./contexts/LoadingContext.tsx";
import { VaultProvider } from "./vault/VaultProvider.tsx";
import { plaintextProjection } from "./vault/plaintextProjection.ts";

const useStyles = makeStyles({
  root: {
    minHeight: "100vh",
  },
});

function App() {
  const styles = useStyles();

  const { resolved } = useColorScheme();

  const projection = useMemo(() => plaintextProjection(), []);

  useEffect(() => {
    document.documentElement.dataset.theme = resolved;
  }, [resolved]);

  return (
    <FluentProvider
      className={styles.root}
      data-theme={resolved}
      theme={resolved === "dark" ? darkTheme : lightTheme}>
      <GlobalStyles />
      <VaultProvider projection={projection}>
        <LoadingContextProvider>
          <Routes>
            <Route path="/" element={<Layout />}>
              <Route index element={<Overview />} />
              <Route path="/transactions" element={<Transactions />} />
              <Route path="/transactions/new" element={<Transactions />} />
              <Route path="/transactions/:id/edit" element={<Transactions />} />
              <Route path="/budgets" element={<Budgets />} />
              <Route path="/settings" element={<Settings />} />
            </Route>
          </Routes>
        </LoadingContextProvider>
      </VaultProvider>
    </FluentProvider>
  );
}

export default App;
