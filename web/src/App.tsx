import "@fontsource/lato/300.css";
import "@fontsource/lato/400.css";
import "@fontsource/lato/700.css";
import "./App.css";

import { Route, Routes } from "react-router-dom";
import Dashboard from "./pages/Dashboard/Dashboard.tsx";
import Transactions from "./pages/Transactions/Transactions.tsx";
import Settings from "./pages/Settings/Settings.tsx";
import Layout from "./pages/Layout.tsx";
import { createTheme, CssBaseline, ThemeProvider } from "@mui/material";
import { LocalizationProvider } from "@mui/x-date-pickers";
import { AdapterDayjs } from "@mui/x-date-pickers/AdapterDayjs";
import { LoadingContextProvider } from "./contexts/LoadingContext.tsx";
import { CalendarContextProvider } from "./contexts/CalendarContext.tsx";
import { TransactionUtilitiesContextProvider } from "./contexts/TransactionUtilitiesContext.tsx";

function App() {
  const theme = createTheme({
    typography: {
      fontFamily: `"Lato","Roboto", "Helvetica", "Arial", sans-serif`,
      fontSize: 14,
      fontWeightLight: 300,
      fontWeightRegular: 400,
      fontWeightMedium: 500,
    },
    breakpoints: {
      values: {
        xs: 0,
        sm: 600,
        md: 900,
        lg: 1200,
        xl: 2000,
      },
    },
  });
  return (
    <>
      <CssBaseline />
      <ThemeProvider theme={theme}>
        <LocalizationProvider dateAdapter={AdapterDayjs}>
          <LoadingContextProvider>
            <CalendarContextProvider>
              <TransactionUtilitiesContextProvider>
                <Routes>
                  <Route path="/" element={<Layout />}>
                    <Route index element={<Dashboard />} />
                    <Route path="/transactions" element={<Transactions />} />
                    <Route path="/settings" element={<Settings />} />
                  </Route>
                </Routes>
              </TransactionUtilitiesContextProvider>
            </CalendarContextProvider>
          </LoadingContextProvider>
        </LocalizationProvider>
      </ThemeProvider>
    </>
  );
}

export default App;
