import './App.css'
import '@fontsource/roboto/300.css';
import '@fontsource/roboto/400.css';
import '@fontsource/roboto/500.css';
import '@fontsource/roboto/700.css';
import {Route, Routes} from "react-router-dom";
import Expenses from "./pages/Expenses/Expenses.tsx";
import Accounts from "./pages/Accounts/Accounts.tsx";
import Dashboard from "./pages/Dashboard/Dashboard.tsx";
import Categories from "./pages/Categories/Categories.tsx";
import Income from "./pages/Income/Income.tsx";
import Layout from "./pages/Layout.tsx";

function App() {

    return (
        <>
            <Routes>
                <Route path="/" element={<Layout/>}>
                    <Route index element={<Dashboard/>}/>
                    <Route path="/accounts" element={<Accounts/>}/>
                    <Route path="/categories" element={<Categories/>}/>
                    <Route path="/expenses" element={<Expenses/>}/>
                    <Route path="/income" element={<Income/>}/>
                </Route>
            </Routes>
        </>
    )
}

export default App
