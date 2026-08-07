import { Outlet } from "react-router";
import { Container, Grid } from "@mui/material";
import NavigationBar from "../components/NavigationBar/NavigationBar.tsx";

const Layout = () => {
  return (
    <Grid container>
      <Grid size={12} sx={{ height: "4rem" }}>
        <NavigationBar />
      </Grid>
      <Grid size={12} sx={{ height: "calc(100vh - 4rem)", width: "100%" }}>
        <LayoutContent />
      </Grid>
    </Grid>
  );
};

const LayoutContent = () => {
  return (
    <Container maxWidth={false} sx={{ backgroundColor: "background.default" }}>
      <Grid
        container
        sx={{
          px: 2,
          backgroundColor: "background.default",
          height: "100%"
        }}>
        <Grid
          container
          sx={{
            my: 2,
            padding: 0,
            backgroundColor: "background.paper",
            borderRadius: 1,
            height: "calc(100vh - 6rem)",
            overflowY: "scroll"
          }}>
          <Outlet />
        </Grid>
      </Grid>
    </Container>
  );
};
export default Layout;
