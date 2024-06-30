import NavigationBar from "../components/NavigationBar/NavigationBar.tsx";
import {Outlet} from "react-router-dom";
import {Container, Grid} from "@mui/material";

const Layout = () => {
    return <>
        <Grid container spacing={3}>
            <Grid item xs={12}>
                <NavigationBar/>
            </Grid>
            <Grid item xs={12}>
                <Container maxWidth="lg">
                    <Outlet/>
                </Container>
            </Grid>
        </Grid>
    </>
}

export default Layout;