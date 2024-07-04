import '@fontsource/lato/300.css';
import '@fontsource/lato/400.css';
import '@fontsource/lato/700.css';
import './App.css'

import {Route, Routes} from "react-router-dom";
import Dashboard from "./pages/Dashboard/Dashboard.tsx";
import Transactions from "./pages/Transactions/Transactions.tsx";
import Settings from "./pages/Settings/Settings.tsx";
import Layout from "./pages/Layout.tsx";
import {createTheme, CssBaseline, ThemeProvider} from "@mui/material";

function App() {
    const theme = createTheme({
        typography: {
            "fontFamily": `"Lato","Roboto", "Helvetica", "Arial", sans-serif`,
            "fontSize": 14,
            "fontWeightLight": 300,
            "fontWeightRegular": 400,
            "fontWeightMedium": 500
        }
    });
    return (
        <>
            <CssBaseline/>
            <ThemeProvider theme={theme}>
                <Routes>
                    <Route path="/" element={<Layout/>}>
                        <Route index element={<Dashboard/>}/>
                        <Route path="/transactions" element={<Transactions/>}/>
                        <Route path="/settings" element={<Settings/>}/>
                    </Route>
                </Routes>
            </ThemeProvider>
        </>
    )
}

export default App
