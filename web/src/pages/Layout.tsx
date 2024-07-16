import { Outlet } from "react-router-dom";
import { Container, Grid, useMediaQuery, useTheme } from "@mui/material";
import NavigationBar from "../components/NavigationBar/NavigationBar.tsx";
import { LoadingContextProvider } from "../contexts/LoadingContext.tsx";
import SideBar from "../components/SideBar/SideBar.tsx";
import { CalendarContextProvider } from "../contexts/CalendarContext.tsx";
import { useState } from "react";
import MiniSideBar from "../components/SideBar/MiniSideBar/MiniSideBar.tsx";

const Layout = () => {
  const theme = useTheme();
  const matchLG = useMediaQuery(theme.breakpoints.up("md"));
  const [sideBarVisible, setSideBarVisibility] = useState(true);

  const handleHideSideBar = (visible: boolean) => {
    setSideBarVisibility(visible);
  };
  return (
    <>
      <LoadingContextProvider>
        <CalendarContextProvider>
          <Grid container>
            <Grid item xs={12} height={64}>
              <NavigationBar />
            </Grid>
            {matchLG && (
              <>
                {sideBarVisible ? (
                  <Grid item sm={12} md={3} xl={1} lg={2} height={"calc(100vh - 64px)"}>
                    <SideBar onVisibilityChange={handleHideSideBar} />
                  </Grid>
                ) : (
                  <Grid item md={1} xl={1} lg={1} height={"calc(100vh - 64px)"}>
                    <MiniSideBar onVisibilityChange={handleHideSideBar} />
                  </Grid>
                )}
                <Grid
                  item
                  sm={12}
                  md={sideBarVisible ? 9 : 11}
                  xl={sideBarVisible ? 11 : 11}
                  lg={sideBarVisible ? 10 : 11}
                  height={"calc(100vh - 64px)"}
                >
                  <Container maxWidth={false} sx={{ padding: 0, margin: 0, marginRight: "16px" }}>
                    <Grid container px={4} ml={-0.5} sx={{ backgroundColor: "#eef2f6", borderRadius: "16px" }}>
                      <Grid
                        container
                        spacing={2}
                        px={4}
                        mx={-0.5}
                        my={3.2}
                        sx={{
                          backgroundColor: "white",
                          borderRadius: "16px",
                          height: "calc(100vh - 128px)",
                          overflowY: "scroll",
                        }}
                      >
                        <Outlet />
                      </Grid>
                    </Grid>
                  </Container>
                </Grid>
              </>
            )}
            {!matchLG && (
              <>
                <Grid item xs={12} height={"calc(100vh - 64px)"}>
                  <Container maxWidth={false} sx={{ padding: 0, margin: 0, marginRight: "16px" }}>
                    <Grid container px={4} ml={-0.5} sx={{ backgroundColor: "#eef2f6", borderRadius: "16px" }}>
                      <Grid
                        container
                        spacing={2}
                        px={4}
                        mx={-0.5}
                        my={3.2}
                        sx={{
                          backgroundColor: "white",
                          borderRadius: "16px",
                          height: "calc(100vh - 128px)",
                          overflowY: "scroll",
                        }}
                      >
                        <Outlet />
                      </Grid>
                    </Grid>
                  </Container>
                </Grid>
              </>
            )}
          </Grid>
        </CalendarContextProvider>
      </LoadingContextProvider>
    </>
  );
};

export default Layout;
