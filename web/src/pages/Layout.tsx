import { Outlet } from "react-router-dom";
import { Container, Grid } from "@mui/material";
import NavigationBar from "../components/NavigationBar/NavigationBar.tsx";

const Layout = () => {
    return <>
        <Grid container>

            <Grid item xs={12}>
                <NavigationBar />
            </Grid>
            <Grid item xs={12}>
                <Container maxWidth={false}>
                    <Outlet />
                </Container>
            </Grid>
        </Grid>
    </>
}

export default Layout;