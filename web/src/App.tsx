import "./App.css";

import { Route, Routes } from "react-router";
import Overview from "./pages/Overview/Overview.tsx";
import Transactions from "./pages/Transactions/Transactions.tsx";
import Budgets from "./pages/Budgets/Budgets.tsx";
import Settings from "./pages/Settings/Settings.tsx";
import Layout from "./pages/Layout.tsx";
import { FluentProvider } from "@fluentui/react-components";
import { darkTheme, lightTheme } from "./fluent/theme.ts";
import { useColorScheme } from "./fluent/useColorScheme.ts";
import { LocalizationProvider } from "@mui/x-date-pickers";
import { AdapterDayjs } from "@mui/x-date-pickers/AdapterDayjs";
import { LoadingContextProvider } from "./contexts/LoadingContext.tsx";
import { TransactionUtilitiesContextProvider } from "./contexts/TransactionUtilitiesContext.tsx";
import axios from "axios";

function App() {
  axios.defaults.baseURL = "http://localhost:4000/";

  const { resolved } = useColorScheme();

  return (
    <FluentProvider theme={resolved === "dark" ? darkTheme : lightTheme}>
      <div style={{ minHeight: "100vh" }}>
      <LocalizationProvider dateAdapter={AdapterDayjs}>
        <LoadingContextProvider>
          <TransactionUtilitiesContextProvider>
            <Routes>
              <Route path="/" element={<Layout />}>
                <Route index element={<Overview />} />
                <Route path="/transactions" element={<Transactions />} />
                <Route path="/budgets" element={<Budgets />} />
                <Route path="/settings" element={<Settings />} />
              </Route>
            </Routes>
          </TransactionUtilitiesContextProvider>
        </LoadingContextProvider>
      </LocalizationProvider>
      </div>
    </FluentProvider>
  );
}

export default App;
