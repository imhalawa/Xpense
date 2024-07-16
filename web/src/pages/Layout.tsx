import { Outlet } from "react-router-dom";
import { Box, Container, Grid, Slide, useMediaQuery, useTheme } from "@mui/material";
import NavigationBar from "../components/NavigationBar/NavigationBar.tsx";
import { LoadingContextProvider } from "../contexts/LoadingContext.tsx";
import SideBar from "../components/SideBar/SideBar.tsx";
import { CalendarContextProvider } from "../contexts/CalendarContext.tsx";
import { useState } from "react";
import MiniSideBar from "../components/SideBar/MiniSideBar/MiniSideBar.tsx";

const Layout = () => {
  const theme = useTheme();
  const matchMD = useMediaQuery(theme.breakpoints.up("md"));

  return (
    <>
      <LoadingContextProvider>
        <CalendarContextProvider>
          <Grid container>
            <Grid item xs={12} height={"64px"}>
              <NavigationBar />
            </Grid>
            {matchMD && <MediumScreensContent />}
            {!matchMD && <SmallScreensContent />}
          </Grid>
        </CalendarContextProvider>
      </LoadingContextProvider>
    </>
  );
};

const SmallScreensContent = () => {
  return (
    <>
      <Grid item xs={12} height={"calc(100vh - 4rem)"} width={"100%"}>
        <LayoutContent />
      </Grid>
    </>
  );
};

const MediumScreensContent = () => {
  const [sideBarVisibility, setSideBarVisibility] = useState(true);

  const handleHideSideBar = (visible: boolean) => {
    setSideBarVisibility(visible);
  };
  return (
    <Box display="flex" justifyContent={"space-between"}>
      {sideBarVisibility && (
        <Box height={"calc(100vh - 4rem)"} width={"8.28rem"}>
          <SideBar onVisibilityChange={handleHideSideBar} />
        </Box>
      )}
      {!sideBarVisibility && (
        <Box height={"calc(100vh - 4rem)"} width={"1.25rem"}>
          <MiniSideBar onVisibilityChange={handleHideSideBar} />
        </Box>
      )}
      <Box
        width={sideBarVisibility ? "calc(100vw - 17rem)" : "calc(100vw - 4.5rem)"}
        height={"calc(100vh - 4rem)"}
        marginLeft={sideBarVisibility ? "8rem" : "3rem"}
      >
        <LayoutContent />
      </Box>
    </Box>
  );
};
const LayoutContent = () => {
  return (
    <Container maxWidth={false} sx={{ padding: 0, margin: 0, height: "calc(100vh - 7rem)" }}>
      <Grid
        container
        px={2.5}
        width={"100%"}
        mx={-0.5}
        sx={{ backgroundColor: "#eef2f6", borderRadius: "1rem", height: "calc(100vh - 7rem)" }}
      >
        <Grid
          container
          mx={0}
          my={2.5}
          sx={{
            backgroundColor: "white",
            borderRadius: "1rem",
            height: "calc(100vh - 9.5rem)",
            overflowY: "scroll",
          }}
        >
          <Outlet />
        </Grid>
      </Grid>
    </Container>
  );
};
export default Layout;
