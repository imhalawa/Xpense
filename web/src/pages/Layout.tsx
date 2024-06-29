import NavigationBar from "../components/NavigationBar/NavigationBar.tsx";
import {Outlet} from "react-router-dom";

const Layout = () => {
    return <>
        <NavigationBar/>
        <Outlet/>
    </>
}

export default Layout;