import { Outlet } from "react-router-dom";
import { Container, Grid } from "@mui/material";
import NavigationBar from "../components/NavigationBar/NavigationBar.tsx";
import { LoadingProvider } from "../contexts/Loading.tsx";

const Layout = () => {
  return (
    <>
      <LoadingProvider>
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
      </LoadingProvider>
    </>
  );
};

export default Layout;
