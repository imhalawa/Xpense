import { Outlet } from "react-router";
import Box from "@mui/material/Box";
import Container from "@mui/material/Container";
import Toolbar from "@mui/material/Toolbar";
import NavigationBar from "../components/NavigationBar/NavigationBar.tsx";

const Layout = () => {
  return (
    <Box sx={{ minHeight: "100vh", backgroundColor: "background.default" }}>
      <NavigationBar />
      <Toolbar />
      <Container component="main" maxWidth="lg" sx={{ paddingY: { xs: 3, md: 4 } }}>
        <Outlet />
      </Container>
    </Box>
  );
};

export default Layout;
