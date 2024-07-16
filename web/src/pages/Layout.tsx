import { Outlet } from "react-router-dom";
import { Box, Container, Grid, useMediaQuery } from "@mui/material";
import NavigationBar from "../components/NavigationBar/NavigationBar.tsx";
import { LoadingProvider } from "../contexts/Loading.tsx";
import SideBar from "../components/SideBar/SideBar.tsx";

const Layout = () => {
  const matchMd = useMediaQuery("(min-width: 768px)");
  return (
    <>
      <LoadingProvider>
        <Grid container>
          <Grid item xs={12} height={64}>
            <NavigationBar />
          </Grid>
          {matchMd && (
            <>
              <Grid item sm={4} md={3} xl={1} lg={2} height={"calc(100vh - 64px)"}>
                <SideBar />
              </Grid>
              <Grid item sm={8} md={9} xl={11} lg={10} height={"calc(100vh - 64px)"}>
                <Container maxWidth={false} sx={{ padding: 0, margin: 0, marginRight: "16px" }}>
                  <Grid container px={4} ml={-0.5} sx={{ backgroundColor: "#eef2f6", borderRadius: "16px" }}>
                    <Grid
                      container
                      spacing={2}
                      px={4}
                      mx={-0.5}
                      my={3.2}
                      sx={{ backgroundColor: "white", borderRadius: "16px", height: "calc(100vh - 128px)", overflowY: "scroll" }}
                    >
                      <Outlet />
                    </Grid>
                  </Grid>
                </Container>
              </Grid>
            </>
          )}
          {!matchMd && (
            <>
              <Grid item xs={12} height={"calc(100vh - 64px)"}>
                <Box sx={{ borderRadius: "16px" }}>
                  <Container maxWidth={false}>
                    <Outlet />
                  </Container>
                </Box>
              </Grid>
            </>
          )}
        </Grid>
      </LoadingProvider>
    </>
  );
};

export default Layout;
