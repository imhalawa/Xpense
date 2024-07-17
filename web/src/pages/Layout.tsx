import { Outlet } from "react-router-dom";
import { Box, Container, Grid, useMediaQuery, useTheme } from "@mui/material";
import NavigationBar from "../components/NavigationBar/NavigationBar.tsx";
import UtilitiesBar from "../components/UtilitiesBar/UtilitiesBar.tsx";
import { useState } from "react";
import SideBar from "../components/SideBar/SideBar.tsx";

const Layout = () => {
  const theme = useTheme();
  const matchMD = useMediaQuery(theme.breakpoints.up("md"));

  return (
    <>
      <Grid container>
        <Grid item xs={12} height={"4rem"}>
          <NavigationBar />
        </Grid>
        {matchMD && <MediumScreensContent />}
        {!matchMD && <SmallScreensContent />}
      </Grid>
    </>
  );
};

const SmallScreensContent = () => {
  return (
    <Grid item xs={12} height={"calc(100vh - 4rem)"} width={"100%"}>
      <LayoutContent />
    </Grid>
  );
};

const MediumScreensContent = () => {
  const [showUtilitiesBar, setSideBarVisibility] = useState(true);

  const handleHideSideBar = (visible: boolean) => {
    setSideBarVisibility(visible);
  };

  return (
    <Box display="flex" justifyContent={"space-between"}>
      {showUtilitiesBar && <UtilitiesBar visible={showUtilitiesBar} onVisibilityChange={handleHideSideBar} />}
      {!showUtilitiesBar && <SideBar visible={!showUtilitiesBar} onVisibilityChange={handleHideSideBar} />}
      <Box
        width={showUtilitiesBar ? "calc(100vw - 26rem)" : "calc(100vw - 4rem)"}
        marginLeft={showUtilitiesBar ? "26rem" : "4rem"}
      >
        <LayoutContent />
      </Box>
    </Box>
  );
};
const LayoutContent = () => {
  return (
    <Container maxWidth={false} sx={{ backgroundColor: "#eef2f6" }}>
      <Grid container px={2} sx={{ backgroundColor: "#eef2f6", height: "100%" }}>
        <Grid
          container
          my={2}
          sx={{
            padding: 0,
            backgroundColor: "white",
            borderRadius: "1rem",
            height: "calc(100vh - 6rem)",
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
